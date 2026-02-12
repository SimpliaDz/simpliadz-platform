using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Exceptions;

namespace SimpliaDz.Inventory.Core.Entities.Stock;

public sealed class StockItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid VariantId { get; private set; }
    public ProductVariant Variant { get; private set; } = default!;
    public Guid LocationId { get; private set; }
    public InventoryLocation Location { get; private set; } = default!;
    public int QuantityOnHand { get; private set; }
    public int? ReorderPoint { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;


    private StockItem() { }

    public StockItem(Guid variantId, Guid locationId, int initialQty = 0)
    {
        if (initialQty < 0)
            throw new ArgumentOutOfRangeException(nameof(initialQty), "Initial quantity cannot be negative.");

        VariantId = variantId;
        LocationId = locationId;
        QuantityOnHand = initialQty;
    }



    public void ApplyDelta(int delta)
    {
        var newQty = QuantityOnHand + delta;
        if (newQty < 0)
            throw new InsufficientStockException();

        QuantityOnHand = newQty;
        Touch();
    }

    public void SetQuantity(int newQty)
    {
        if (newQty < 0)
            throw new ArgumentOutOfRangeException(nameof(newQty), "Quantity cannot be negative.");

        QuantityOnHand = newQty;
        Touch();
    }

    public void SetReorderPoint(int? reorderPoint)
    {
        if (reorderPoint is < 0) throw new ArgumentOutOfRangeException(nameof(reorderPoint));
        ReorderPoint = reorderPoint;

        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

}
