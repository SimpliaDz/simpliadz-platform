using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.UseCases.Locations.Abstractions;
using SimpliaDz.Inventory.UseCases.Services;

namespace SimpliaDz.Inventory.Infrastructure.Services;

internal sealed class StockService : IStockService
{
    private readonly InventoryDbContext _db;
    private readonly ILocationRepository _locations;

    public StockService(InventoryDbContext db, ILocationRepository locations)
    {
        _db = db;
        _locations = locations;
    }

    public async Task ReceiveAsync(
        Guid variantId,
        Guid locationId,
        int quantity,
        string? reference,
        string? note,
        CancellationToken ct)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Received quantity must be greater than zero.");

        await EnsureVariantAndActiveLocationAsync(variantId, locationId, ct);

        var stockItem = await GetOrCreateStockItemAsync(variantId, locationId, ct);
        stockItem.ApplyDelta(quantity);

        _db.StockMovements.Add(new StockMovement(
            variantId,
            locationId,
            StockMovementType.Receipt,
            quantity,
            reference,
            note));

        await _db.SaveChangesAsync(ct);
    }

    public async Task AdjustToAsync(
        Guid variantId,
        Guid locationId,
        int newQuantity,
        string? note,
        CancellationToken ct)
    {
        if (newQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(newQuantity), "Quantity cannot be negative.");

        await EnsureVariantAndActiveLocationAsync(variantId, locationId, ct);

        var stockItem = await GetOrCreateStockItemAsync(variantId, locationId, ct);
        var delta = newQuantity - stockItem.QuantityOnHand;
        if (delta == 0) return;

        stockItem.SetQuantity(newQuantity);
        _db.StockMovements.Add(new StockMovement(
            variantId,
            locationId,
            StockMovementType.Adjustment,
            delta,
            note: note));

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureVariantAndActiveLocationAsync(
        Guid variantId,
        Guid locationId,
        CancellationToken ct)
    {
        if (!await _locations.IsActiveAsync(locationId, ct))
            throw new NotFoundException("Active location", locationId);

        if (!await _db.ProductVariants.AnyAsync(variant => variant.Id == variantId && variant.IsActive, ct))
            throw new NotFoundException("Active product variant", variantId);
    }

    private async Task<StockItem> GetOrCreateStockItemAsync(
        Guid variantId,
        Guid locationId,
        CancellationToken ct)
    {
        var stockItem = await _db.StockItems.FirstOrDefaultAsync(
            item => item.VariantId == variantId && item.LocationId == locationId,
            ct);

        if (stockItem is not null) return stockItem;

        stockItem = new StockItem(variantId, locationId);
        _db.StockItems.Add(stockItem);
        return stockItem;
    }
}
