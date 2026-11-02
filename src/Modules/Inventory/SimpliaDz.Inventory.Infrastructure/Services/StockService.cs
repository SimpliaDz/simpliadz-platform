using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.UseCases.Services;

namespace SimpliaDz.Inventory.Infrastructure.Services;

internal sealed class StockService(InventoryDbContext db) : IStockService
{
    public async Task<IReadOnlyList<StockBalanceDto>> GetByVariantAsync(Guid variantId, CancellationToken ct)
    {
        if (!await db.ProductVariants.AnyAsync(v => v.Id == variantId, ct))
            throw new NotFoundException("Product variant", variantId);

        return await db.StockBalances.AsNoTracking()
            .Where(b => b.VariantId == variantId)
            .OrderBy(b => b.LocationId)
            .Select(b => new StockBalanceDto(b.VariantId, b.LocationId, b.QuantityOnHand, b.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockBalanceDto>> GetByLocationAsync(Guid locationId, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            throw new NotFoundException("Location", locationId);

        return await db.StockBalances.AsNoTracking()
            .Where(b => b.LocationId == locationId)
            .OrderBy(b => b.VariantId)
            .Select(b => new StockBalanceDto(b.VariantId, b.LocationId, b.QuantityOnHand, b.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalByVariantAsync(Guid variantId, CancellationToken ct)
    {
        if (!await db.ProductVariants.AnyAsync(v => v.Id == variantId, ct))
            throw new NotFoundException("Product variant", variantId);

        return await db.StockBalances.AsNoTracking()
            .Where(b => b.VariantId == variantId)
            .SumAsync(b => (int?)b.QuantityOnHand, ct) ?? 0;
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(Guid? variantId,
        Guid? locationId, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be positive and page size must be 1 to 100.");

        var query = db.StockMovements.AsNoTracking().AsQueryable();
        if (variantId.HasValue) query = query.Where(m => m.VariantId == variantId.Value);
        if (locationId.HasValue) query = query.Where(m => m.LocationId == locationId.Value);

        var rows = await query.OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.Id)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .ToListAsync(ct);

        return rows.Select(m => new StockMovementDto(m.Id, m.VariantId, m.LocationId,
            m.QuantityDelta, m.MovementType.ToString(), m.Reference, m.ReferenceType,
            m.ReferenceId, m.Reason, m.OccurredAt, m.CreatedBy)).ToList();
    }

    public async Task<OpeningStockResult> OpenAsync(Guid variantId, Guid locationId, int quantity,
        string? reason, string createdBy, CancellationToken ct)
    {
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Opening quantity cannot be negative.");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                await EnsureActivePairAsync(variantId, locationId, ct);
                if (await db.StockBalances.AnyAsync(b =>
                        b.VariantId == variantId && b.LocationId == locationId, ct) ||
                    await db.StockMovements.AnyAsync(m =>
                        m.VariantId == variantId && m.LocationId == locationId, ct))
                    throw new ConflictException("Opening stock already exists or stock history has begun for this variant and location.");

                var balance = new StockBalance(variantId, locationId);
                if (quantity > 0) balance.ApplyDelta(quantity);
                var movement = new StockMovement(variantId, locationId,
                    StockMovementType.OpeningBalance, quantity, createdBy, reason: reason);
                db.StockBalances.Add(balance);
                db.StockMovements.Add(movement);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new OpeningStockResult(variantId, locationId, quantity,
                    movement.Id, movement.OccurredAt, movement.CreatedBy);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                throw new ConflictException("Opening stock already exists for this variant and location.");
            }
            catch (Exception exception) when (attempt < 4 && IsRetryable(exception))
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
        }

        throw new ConflictException("Opening stock could not be completed due to concurrent updates.");
    }

    public Task ReceiveAsync(Guid variantId, Guid locationId, int quantity, string? reference,
        string? reason, string? referenceType, Guid? referenceId, string createdBy, CancellationToken ct)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Received quantity must be greater than zero.");

        return ChangeAsync(variantId, locationId, quantity, null, StockMovementType.PurchaseReceipt,
            reference, reason, referenceType, referenceId, createdBy, ct);
    }

    public Task AdjustToAsync(Guid variantId, Guid locationId, int newQuantity, string reason,
        string createdBy, CancellationToken ct)
    {
        if (newQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(newQuantity), "Quantity cannot be negative.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Adjustment reason is required.", nameof(reason));

        return ChangeAsync(variantId, locationId, null, newQuantity, StockMovementType.Adjustment,
            null, reason, null, null, createdBy, ct);
    }

    private async Task ChangeAsync(Guid variantId, Guid locationId, int? receiptQuantity,
        int? targetQuantity, StockMovementType type, string? reference, string? reason,
        string? referenceType, Guid? referenceId, string createdBy, CancellationToken ct)
    {
        // Serializable protects the read/create path. RowVersion detects competing writes;
        // retry the whole operation so its movement always matches the committed delta.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                await EnsureActivePairAsync(variantId, locationId, ct);

                var balance = await db.StockBalances.FirstOrDefaultAsync(
                    b => b.VariantId == variantId && b.LocationId == locationId, ct);
                var current = balance?.QuantityOnHand ?? 0;
                var delta = receiptQuantity ?? checked(targetQuantity!.Value - current);
                if (delta == 0) return;

                if (balance is null)
                {
                    balance = new StockBalance(variantId, locationId);
                    db.StockBalances.Add(balance);
                }

                balance.ApplyDelta(delta);
                db.StockMovements.Add(new StockMovement(variantId, locationId, type, delta,
                    createdBy, reference, reason, referenceType, referenceId));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }
            catch (Exception exception) when (attempt < 4 && IsRetryable(exception))
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
    }

    private static bool IsRetryable(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } } ||
        exception is SqlException { Number: 1205 };

    private async Task EnsureActivePairAsync(Guid variantId, Guid locationId, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId && l.IsActive, ct))
            throw new NotFoundException("Active location", locationId);
        if (!await db.ProductVariants.AnyAsync(v => v.Id == variantId && v.IsActive, ct))
            throw new NotFoundException("Active product variant", variantId);
    }
}
