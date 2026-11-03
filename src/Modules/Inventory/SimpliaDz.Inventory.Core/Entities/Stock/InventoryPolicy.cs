using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Core.Entities.Stock;

public sealed class InventoryPolicy
{
    public Guid ProductVariantId { get; private set; }
    public ProductVariant ProductVariant { get; private set; } = default!;
    public Guid LocationId { get; private set; }
    public InventoryLocation Location { get; private set; } = default!;
    public int ReorderPoint { get; private set; }
    public int? TargetStockLevel { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; private set; } = [];

    private InventoryPolicy() { }

    public InventoryPolicy(Guid productVariantId, Guid locationId,
        int reorderPoint, int? targetStockLevel)
    {
        if (productVariantId == Guid.Empty)
            throw new ArgumentException("Product variant is required.", nameof(productVariantId));
        if (locationId == Guid.Empty)
            throw new ArgumentException("Location is required.", nameof(locationId));
        ProductVariantId = productVariantId;
        LocationId = locationId;
        Configure(reorderPoint, targetStockLevel);
    }

    public void Configure(int reorderPoint, int? targetStockLevel)
    {
        if (reorderPoint < 0)
            throw new ArgumentOutOfRangeException(nameof(reorderPoint));
        if (targetStockLevel < reorderPoint)
            throw new ArgumentOutOfRangeException(nameof(targetStockLevel),
                "Target stock level must be at least the reorder point.");
        ReorderPoint = reorderPoint;
        TargetStockLevel = targetStockLevel;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
