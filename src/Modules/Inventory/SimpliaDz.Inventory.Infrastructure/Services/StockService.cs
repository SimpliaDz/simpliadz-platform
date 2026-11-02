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

    public Task ReceiveAsync(Guid variantId, Guid locationId, int quantity, string? reference,
        string? note, CancellationToken ct)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Received quantity must be greater than zero.");

        return ChangeAsync(variantId, locationId, quantity, null, StockMovementType.Receipt, reference, note, ct);
    }

    public Task AdjustToAsync(Guid variantId, Guid locationId, int newQuantity, string? note, CancellationToken ct)
    {
        if (newQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(newQuantity), "Quantity cannot be negative.");

        return ChangeAsync(variantId, locationId, null, newQuantity, StockMovementType.Adjustment, null, note, ct);
    }

    private async Task ChangeAsync(Guid variantId, Guid locationId, int? receiptQuantity,
        int? targetQuantity, StockMovementType type, string? reference, string? note, CancellationToken ct)
    {
        // Serializable protects the read/create path. RowVersion detects competing writes;
        // retry the whole operation so its movement always matches the committed delta.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId && l.IsActive, ct))
                    throw new NotFoundException("Active location", locationId);
                if (!await db.ProductVariants.AnyAsync(v => v.Id == variantId && v.IsActive, ct))
                    throw new NotFoundException("Active product variant", variantId);

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
                db.StockMovements.Add(new StockMovement(variantId, locationId, type, delta, reference, note));
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
}
