using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.UseCases.Services;

namespace SimpliaDz.Inventory.Infrastructure.Services;

public sealed class InventoryCountService(InventoryDbContext db) : IInventoryCountService
{
    public async Task<InventoryCountDto> StartAsync(Guid locationId, string createdBy, CancellationToken ct)
    {
        if (locationId == Guid.Empty) throw new ArgumentException("Location is required.", nameof(locationId));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Locations.AnyAsync(x => x.Id == locationId && x.IsActive, ct))
            throw new NotFoundException("Active location", locationId);
        var balances = await db.StockBalances.AsNoTracking()
            .Where(x => x.LocationId == locationId).OrderBy(x => x.VariantId).ToListAsync(ct);
        var count = new InventoryCount(locationId, createdBy);
        foreach (var balance in balances)
            count.AddLine(balance.VariantId, balance.QuantityOnHand);
        db.InventoryCounts.Add(count);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(count);
    }

    public async Task<InventoryCountDto> AddVariantAsync(Guid countId, Guid productVariantId,
        CancellationToken ct)
    {
        if (productVariantId == Guid.Empty)
            throw new ArgumentException("Product variant is required.", nameof(productVariantId));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var count = await LoadAsync(countId, ct);
            EnsureDraft(count);
            if (count.Lines.Any(x => x.ProductVariantId == productVariantId))
                throw new ConflictException("Variant is already in this inventory count.");
            if (!await db.ProductVariants.AnyAsync(x => x.Id == productVariantId && x.IsActive, ct))
                throw new NotFoundException("Active product variant", productVariantId);
            var expected = await db.StockBalances.AsNoTracking()
                .Where(x => x.LocationId == count.LocationId && x.VariantId == productVariantId)
                .Select(x => (int?)x.QuantityOnHand).SingleOrDefaultAsync(ct) ?? 0;
            var line = count.AddLine(productVariantId, expected);
            db.InventoryCountLines.Add(line);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(count);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw new ConflictException("Inventory count changed concurrently; reload and retry.");
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw new ConflictException("Variant is already in this inventory count.");
        }
    }

    public async Task<InventoryCountDto> SetCountedQuantityAsync(Guid countId,
        Guid productVariantId, int countedQuantity, CancellationToken ct)
    {
        if (countedQuantity < 0) throw new ArgumentOutOfRangeException(nameof(countedQuantity));
        var count = await LoadAsync(countId, ct);
        EnsureDraft(count);
        if (!count.Lines.Any(x => x.ProductVariantId == productVariantId))
            throw new NotFoundException("Inventory count line", productVariantId);
        count.SetCountedQuantity(productVariantId, countedQuantity);
        await SaveWithConflictAsync(ct);
        return Map(count);
    }

    public async Task<InventoryCountDto> CompleteAsync(Guid countId, string completedBy,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var count = await LoadAsync(countId, ct);
                EnsureDraft(count);
                if (count.Lines.Count == 0 || count.Lines.Any(x => x.CountedQuantity is null))
                    throw new ConflictException("Enter a physical quantity for every count line.");
                if (!await db.Locations.AnyAsync(x => x.Id == count.LocationId && x.IsActive, ct))
                    throw new ConflictException("Count location must still be active.");

                var lines = count.Lines.OrderBy(x => x.ProductVariantId).ToList();
                var variantIds = lines.Select(x => x.ProductVariantId).ToArray();
                var active = await db.ProductVariants.CountAsync(x =>
                    variantIds.Contains(x.Id) && x.IsActive, ct);
                if (active != lines.Count)
                    throw new ConflictException("All counted variants must still be active.");

                var balances = await db.StockBalances
                    .Where(x => x.LocationId == count.LocationId).ToListAsync(ct);
                var byVariant = balances.ToDictionary(x => x.VariantId);
                if (balances.Any(x => !variantIds.Contains(x.VariantId)) ||
                    lines.Any(line => (byVariant.TryGetValue(line.ProductVariantId, out var balance)
                        ? balance.QuantityOnHand : 0) != line.ExpectedQuantity))
                    throw new ConflictException("Stock changed after this count was started; start a new count.");

                for (var index = 0; index < lines.Count; index++)
                {
                    var line = lines[index];
                    var delta = line.Difference!.Value;
                    if (delta == 0) continue;
                    if (!byVariant.TryGetValue(line.ProductVariantId, out var balance))
                    {
                        balance = new StockBalance(line.ProductVariantId, count.LocationId);
                        db.StockBalances.Add(balance);
                        byVariant.Add(line.ProductVariantId, balance);
                    }
                    balance.ApplyDelta(delta);
                    db.StockMovements.Add(new StockMovement(line.ProductVariantId, count.LocationId,
                        StockMovementType.Adjustment, delta, completedBy,
                        reference: $"COUNT-{count.Id:N}", reason: "Physical inventory count",
                        referenceType: "InventoryCount", referenceId: count.Id,
                        sourceLineNumber: index + 1));
                }

                count.Complete(completedBy);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Map(count);
            }
            catch (Exception exception) when (attempt < 4 && IsRetryable(exception))
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }
        throw new ConflictException("Inventory count changed concurrently; retry the request.");
    }

    public async Task CancelAsync(Guid countId, CancellationToken ct)
    {
        var count = await LoadAsync(countId, ct);
        EnsureDraft(count);
        count.Cancel();
        await SaveWithConflictAsync(ct);
    }

    public async Task<InventoryCountDto> GetByIdAsync(Guid countId, CancellationToken ct) =>
        Map(await LoadAsync(countId, ct));

    public async Task<IReadOnlyList<InventoryCountDto>> ListAsync(Guid? locationId, int page,
        int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        var query = db.InventoryCounts.AsNoTracking().Include(x => x.Lines).AsQueryable();
        if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId.Value);
        var counts = await query.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return counts.Select(Map).ToList();
    }

    private async Task<InventoryCount> LoadAsync(Guid id, CancellationToken ct) =>
        await db.InventoryCounts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Inventory count", id);

    private static void EnsureDraft(InventoryCount count)
    {
        if (count.Status != InventoryCountStatus.Draft)
            throw new ConflictException("Only draft inventory counts can be changed.");
    }

    private async Task SaveWithConflictAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("Inventory count changed concurrently; reload and retry.");
        }
    }

    private static bool IsUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool IsRetryable(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } } ||
        exception is SqlException { Number: 1205 };

    private static InventoryCountDto Map(InventoryCount x) => new(x.Id, x.LocationId,
        x.Status.ToString(), x.StartedAt, x.CompletedAt, x.CreatedBy, x.CompletedBy,
        x.Lines.OrderBy(line => line.ProductVariantId)
            .Select(line => new InventoryCountLineDto(line.Id, line.ProductVariantId,
                line.ExpectedQuantity, line.CountedQuantity, line.Difference)).ToList());
}
