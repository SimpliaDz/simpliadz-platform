using Simplia.Inventory.Core.Entities.Catalog;

namespace Simplia.Inventory.Core.Entities.Stock;

public enum InventoryCountStatus
{
    Draft = 1,
    Completed = 2,
    Cancelled = 3
}

public sealed class InventoryCount
{
    private readonly List<InventoryCountLine> _lines = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid LocationId { get; private set; }
    public InventoryLocation Location { get; private set; } = default!;
    public InventoryCountStatus Status { get; private set; } = InventoryCountStatus.Draft;
    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string? CompletedBy { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<InventoryCountLine> Lines => _lines;

    private InventoryCount() { }

    public InventoryCount(Guid locationId, string createdBy)
    {
        if (locationId == Guid.Empty) throw new ArgumentException("Location is required.", nameof(locationId));
        if (string.IsNullOrWhiteSpace(createdBy) || createdBy.Trim().Length > 100)
            throw new ArgumentException("Creator is required and cannot exceed 100 characters.", nameof(createdBy));
        LocationId = locationId;
        CreatedBy = createdBy.Trim();
    }

    public InventoryCountLine AddLine(Guid productVariantId, int expectedQuantity)
    {
        EnsureDraft();
        if (productVariantId == Guid.Empty)
            throw new ArgumentException("Product variant is required.", nameof(productVariantId));
        if (expectedQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedQuantity));
        if (_lines.Any(x => x.ProductVariantId == productVariantId))
            throw new InvalidOperationException("Variant already belongs to this inventory count.");
        var line = new InventoryCountLine(Id, productVariantId, expectedQuantity);
        _lines.Add(line);
        Touch();
        return line;
    }

    public void SetCountedQuantity(Guid productVariantId, int quantity)
    {
        EnsureDraft();
        var line = _lines.FirstOrDefault(x => x.ProductVariantId == productVariantId)
            ?? throw new ArgumentException("Variant does not belong to this count.", nameof(productVariantId));
        line.SetCountedQuantity(quantity);
        Touch();
    }

    public void Complete(string completedBy)
    {
        EnsureDraft();
        if (_lines.Count == 0 || _lines.Any(x => x.CountedQuantity is null))
            throw new InvalidOperationException("Every count line must have a physical quantity before completion.");
        if (string.IsNullOrWhiteSpace(completedBy) || completedBy.Trim().Length > 100)
            throw new ArgumentException("Completing user is required and cannot exceed 100 characters.", nameof(completedBy));
        Status = InventoryCountStatus.Completed;
        CompletedBy = completedBy.Trim();
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Cancel()
    {
        EnsureDraft();
        Status = InventoryCountStatus.Cancelled;
        Touch();
    }

    private void EnsureDraft()
    {
        if (Status != InventoryCountStatus.Draft)
            throw new InvalidOperationException("Only draft inventory counts can be changed.");
    }

    private void Touch() => Revision = checked(Revision + 1);
}

public sealed class InventoryCountLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InventoryCountId { get; private set; }
    public InventoryCount InventoryCount { get; private set; } = default!;
    public Guid ProductVariantId { get; private set; }
    public ProductVariant ProductVariant { get; private set; } = default!;
    public int ExpectedQuantity { get; private set; }
    public int? CountedQuantity { get; private set; }
    public int? Difference => CountedQuantity.HasValue
        ? checked(CountedQuantity.Value - ExpectedQuantity)
        : null;

    private InventoryCountLine() { }

    internal InventoryCountLine(Guid inventoryCountId, Guid productVariantId, int expectedQuantity)
    {
        InventoryCountId = inventoryCountId;
        ProductVariantId = productVariantId;
        ExpectedQuantity = expectedQuantity;
    }

    internal void SetCountedQuantity(int quantity)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        CountedQuantity = quantity;
    }
}
