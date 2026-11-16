namespace SimpliaDz.Inventory.Core.Entities.Stock;

public enum InventoryWarningKind { LowStock = 1, Ageing = 2 }

public sealed class InventoryWarning
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductVariantId { get; private set; }
    public Guid LocationId { get; private set; }
    public InventoryWarningKind Kind { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int? ReorderPoint { get; private set; }
    public DateTimeOffset? StockEnteredAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset FirstDetectedAt { get; private set; }
    public DateTimeOffset LastDetectedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private InventoryWarning() { }

    public InventoryWarning(Guid variantId, Guid locationId, InventoryWarningKind kind,
        int quantityOnHand, int? reorderPoint, DateTimeOffset? stockEnteredAt,
        DateTimeOffset detectedAt)
    {
        if (variantId == Guid.Empty || locationId == Guid.Empty || !Enum.IsDefined(kind))
            throw new ArgumentException("Warning identity is invalid.");
        ProductVariantId = variantId;
        LocationId = locationId;
        Kind = kind;
        Refresh(quantityOnHand, reorderPoint, stockEnteredAt, detectedAt);
    }

    public void Refresh(int quantityOnHand, int? reorderPoint,
        DateTimeOffset? stockEnteredAt, DateTimeOffset detectedAt)
    {
        if (quantityOnHand < 0 || reorderPoint < 0)
            throw new ArgumentOutOfRangeException(nameof(quantityOnHand));
        if (!IsActive) FirstDetectedAt = detectedAt;
        if (FirstDetectedAt == default) FirstDetectedAt = detectedAt;
        QuantityOnHand = quantityOnHand;
        ReorderPoint = reorderPoint;
        StockEnteredAt = stockEnteredAt;
        LastDetectedAt = detectedAt;
        ResolvedAt = null;
        IsActive = true;
    }

    public void Resolve(DateTimeOffset at)
    {
        if (!IsActive) return;
        IsActive = false;
        ResolvedAt = at;
    }
}
