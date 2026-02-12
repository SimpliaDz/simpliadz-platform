using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Core.Entities.Stock;

public enum StockMovementType
{
    Receipt = 1,
    Issue = 2,
    ReturnIn = 3,
    Adjustment = 4,
    TransferIn = 5,
    TransferOut = 6
}

public sealed class StockMovement
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;

    public Guid VariantId { get; private set; }
    public ProductVariant Variant { get; private set; } = default!;

    public Guid LocationId { get; private set; }
    public InventoryLocation Location { get; private set; } = default!;

    // +in / -out
    public int QuantityDelta { get; private set; }

    public StockMovementType Type { get; private set; }

    // e.g. "PO-1001", "SO-2001"
    public string? Reference { get; private set; }

    public string? Note { get; private set; }

    public string? SourceDocumentType { get; private set; } // "StockReceipt", "StockTransfer", ...
    public Guid? SourceDocumentId { get; private set; }
    public int? SourceLineNumber { get; private set; } // optional

    private StockMovement() { }

    public StockMovement(
        Guid variantId,
        Guid locationId,
        StockMovementType type,
        int quantityDelta,
        string? reference = null,
        string? note = null,
        string? sourceDocumentType = null,
        Guid? sourceDocumentId = null,
        int? sourceLineNumber = null
        )
    {
        if (quantityDelta == 0)
            throw new ArgumentException("Quantity delta cannot be zero.", nameof(quantityDelta));

        VariantId = variantId;
        LocationId = locationId;
        Type = type;
        QuantityDelta = quantityDelta;

        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        SourceDocumentType = string.IsNullOrWhiteSpace(sourceDocumentType) ? null : sourceDocumentType.Trim();
        SourceDocumentId = sourceDocumentId;
        SourceLineNumber = sourceLineNumber;
    }
}
