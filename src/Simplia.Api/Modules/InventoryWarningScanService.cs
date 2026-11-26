using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;

namespace Simplia.Api.Modules;

public sealed class InventoryWarningScanService(InventoryDbContext db, TimeProvider clock)
{
    private const string LowStockSql = """
        SELECT p.ProductVariantId, p.LocationId,
               COALESCE(b.QuantityOnHand, 0) AS QuantityOnHand,
               p.ReorderPoint
        FROM inventory.InventoryPolicies AS p
        INNER JOIN inventory.ProductVariants AS v ON v.Id = p.ProductVariantId AND v.IsActive = 1
        INNER JOIN inventory.Products AS product ON product.Id = v.ProductId AND product.IsActive = 1
        INNER JOIN inventory.Locations AS l ON l.Id = p.LocationId AND l.IsActive = 1
        LEFT JOIN inventory.StockItems AS b ON b.VariantId = p.ProductVariantId
            AND b.LocationId = p.LocationId
        WHERE COALESCE(b.QuantityOnHand, 0) <= p.ReorderPoint
        """;

    private const string AgeingSql = """
        WITH Running AS (
            SELECT m.VariantId, m.LocationId, m.QuantityDelta, m.OccurredAt,
                   ROW_NUMBER() OVER (PARTITION BY m.VariantId, m.LocationId
                       ORDER BY m.OccurredAt, m.Id) AS SequenceNumber,
                   SUM(CAST(m.QuantityDelta AS bigint)) OVER
                       (PARTITION BY m.VariantId, m.LocationId
                        ORDER BY m.OccurredAt, m.Id ROWS UNBOUNDED PRECEDING) AS RunningQuantity
            FROM inventory.StockMovements AS m
        ), LastZero AS (
            SELECT VariantId, LocationId, MAX(SequenceNumber) AS LastZeroSequence
            FROM Running WHERE RunningQuantity = 0
            GROUP BY VariantId, LocationId
        ), Anchors AS (
            SELECT b.VariantId AS ProductVariantId, b.LocationId,
                   b.QuantityOnHand, MIN(r.OccurredAt) AS StockEnteredAt
            FROM inventory.StockItems AS b
            INNER JOIN inventory.ProductVariants AS v ON v.Id = b.VariantId AND v.IsActive = 1
            INNER JOIN inventory.Products AS product ON product.Id = v.ProductId AND product.IsActive = 1
            INNER JOIN inventory.Locations AS l ON l.Id = b.LocationId AND l.IsActive = 1
            INNER JOIN Running AS r ON r.VariantId = b.VariantId AND r.LocationId = b.LocationId
            LEFT JOIN LastZero AS z ON z.VariantId = b.VariantId AND z.LocationId = b.LocationId
            WHERE b.QuantityOnHand > 0 AND r.QuantityDelta > 0
              AND r.SequenceNumber > COALESCE(z.LastZeroSequence, 0)
            GROUP BY b.VariantId, b.LocationId, b.QuantityOnHand
        )
        SELECT ProductVariantId, LocationId, QuantityOnHand, StockEnteredAt
        FROM Anchors WHERE StockEnteredAt <= @cutoff
        """;

    public async Task<bool> RunAsync(bool force, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var token = Guid.NewGuid();
        var claim = db.InventoryWarningSettings.Where(x => x.Id == InventoryWarningSettings.SingletonId &&
            (x.LeaseUntil == null || x.LeaseUntil < now));
        if (!force) claim = claim.Where(x => x.Enabled && x.NextRunAt <= now);
        if (await claim.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LeaseToken, token)
                .SetProperty(x => x.LeaseUntil, now.AddMinutes(30)), ct) != 1)
            return false;

        try
        {
            var settings = await db.InventoryWarningSettings.AsNoTracking().SingleAsync(ct);
            var candidates = new List<WarningCandidate>();
            if (settings.LowStockEnabled)
            {
                var low = await db.Database.SqlQueryRaw<LowStockCandidate>(LowStockSql)
                    .ToListAsync(ct);
                candidates.AddRange(low.Select(x => new WarningCandidate(x.ProductVariantId,
                    x.LocationId, InventoryWarningKind.LowStock, x.QuantityOnHand,
                    x.ReorderPoint, null)));
            }
            if (settings.AgeingEnabled)
            {
                var cutoff = now.AddDays(-settings.AgeThresholdDays);
                var age = await db.Database.SqlQueryRaw<AgeingCandidate>(AgeingSql,
                    new SqlParameter("@cutoff", cutoff)).ToListAsync(ct);
                candidates.AddRange(age.Select(x => new WarningCandidate(x.ProductVariantId,
                    x.LocationId, InventoryWarningKind.Ageing, x.QuantityOnHand,
                    null, x.StockEnteredAt)));
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var current = await db.InventoryWarnings.ToListAsync(ct);
            var byKey = current.ToDictionary(x => (x.ProductVariantId, x.LocationId, x.Kind));
            var seen = new HashSet<(Guid, Guid, InventoryWarningKind)>();
            foreach (var candidate in candidates)
            {
                var key = (candidate.VariantId, candidate.LocationId, candidate.Kind);
                seen.Add(key);
                if (byKey.TryGetValue(key, out var warning))
                    warning.Refresh(candidate.QuantityOnHand, candidate.ReorderPoint,
                        candidate.StockEnteredAt, now);
                else
                    db.InventoryWarnings.Add(new InventoryWarning(candidate.VariantId,
                        candidate.LocationId, candidate.Kind, candidate.QuantityOnHand,
                        candidate.ReorderPoint, candidate.StockEnteredAt, now));
            }
            foreach (var warning in current.Where(x => x.IsActive))
                if (!seen.Contains((warning.ProductVariantId, warning.LocationId, warning.Kind)))
                    warning.Resolve(now);
            await db.SaveChangesAsync(ct);
            if (await db.InventoryWarningSettings.Where(x => x.Id ==
                    InventoryWarningSettings.SingletonId && x.LeaseToken == token)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LastRunAt, now)
                    .SetProperty(x => x.NextRunAt, now.AddMinutes(settings.IntervalMinutes))
                    .SetProperty(x => x.LeaseToken, (Guid?)null)
                    .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), ct) != 1)
                throw new InvalidOperationException("Inventory warning lease was lost.");
            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await db.InventoryWarningSettings.Where(x => x.Id ==
                    InventoryWarningSettings.SingletonId && x.LeaseToken == token)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LeaseToken, (Guid?)null)
                    .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), CancellationToken.None);
            throw;
        }
    }

    private sealed record WarningCandidate(Guid VariantId, Guid LocationId,
        InventoryWarningKind Kind, int QuantityOnHand, int? ReorderPoint,
        DateTimeOffset? StockEnteredAt);
    public sealed class LowStockCandidate
    {
        public Guid ProductVariantId { get; set; }
        public Guid LocationId { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReorderPoint { get; set; }
    }
    public sealed class AgeingCandidate
    {
        public Guid ProductVariantId { get; set; }
        public Guid LocationId { get; set; }
        public int QuantityOnHand { get; set; }
        public DateTimeOffset StockEnteredAt { get; set; }
    }
}
