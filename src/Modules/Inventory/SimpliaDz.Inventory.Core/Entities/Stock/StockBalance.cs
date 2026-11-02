using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Exceptions;

namespace SimpliaDz.Inventory.Core.Entities.Stock;

public sealed class StockBalance
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid VariantId { get; private set; }
    public ProductVariant Variant { get; private set; } = default!;
    public Guid LocationId { get; private set; }
    public InventoryLocation Location { get; private set; } = default!;
    public int QuantityOnHand { get; private set; }
    public int? ReorderPoint { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; private set; } = [];


    private StockBalance() { }

    public StockBalance(Guid variantId, Guid locationId)
    {
        VariantId = variantId;
        LocationId = locationId;
    }



    public void ApplyDelta(int delta)
    {
        var newQty = checked(QuantityOnHand + delta);
        if (newQty < 0)
            throw new InsufficientStockException();

        QuantityOnHand = newQty;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

}
