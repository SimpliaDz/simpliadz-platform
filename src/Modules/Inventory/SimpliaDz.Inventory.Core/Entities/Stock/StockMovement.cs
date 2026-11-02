using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Core.Entities.Stock;

public enum StockMovementType
{
    // Keep historical values stable. Existing rows may use these types.
    Receipt = 1,
    Issue = 2,
    ReturnIn = 3,
    Adjustment = 4,
    TransferIn = 5,
    TransferOut = 6,
    OpeningBalance = 7,
    PurchaseReceipt = 8,
    Sale = 9,
    SaleReturn = 10
}

public sealed class StockMovement
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid VariantId { get; private set; }
    public ProductVariant Variant { get; private set; } = default!;
    public Guid LocationId { get; private set; }
    public InventoryLocation Location { get; private set; } = default!;
    public int QuantityDelta { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public string? Reference { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public int? SourceLineNumber { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; private set; } = string.Empty;

    private StockMovement() { }

    public StockMovement(Guid variantId, Guid locationId, StockMovementType movementType,
        int quantityDelta, string createdBy, string? reference = null, string? reason = null,
        string? referenceType = null, Guid? referenceId = null, int? sourceLineNumber = null)
    {
        if (variantId == Guid.Empty) throw new ArgumentException("Variant is required.", nameof(variantId));
        if (locationId == Guid.Empty) throw new ArgumentException("Location is required.", nameof(locationId));
        if (!Enum.IsDefined(movementType))
            throw new ArgumentOutOfRangeException(nameof(movementType));
        if (quantityDelta == 0) throw new ArgumentException("Quantity delta cannot be zero.", nameof(quantityDelta));
        if (RequiresPositiveDelta(movementType) && quantityDelta < 0)
            throw new ArgumentException("This movement type must increase stock.", nameof(quantityDelta));
        if (RequiresNegativeDelta(movementType) && quantityDelta > 0)
            throw new ArgumentException("This movement type must decrease stock.", nameof(quantityDelta));
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("Movement actor is required.", nameof(createdBy));
        if (sourceLineNumber is < 1)
            throw new ArgumentOutOfRangeException(nameof(sourceLineNumber));
        if (referenceId.HasValue != !string.IsNullOrWhiteSpace(referenceType) ||
            referenceId == Guid.Empty)
            throw new ArgumentException("Reference type and a nonempty reference ID must be supplied together.");

        VariantId = variantId;
        LocationId = locationId;
        MovementType = movementType;
        QuantityDelta = quantityDelta;
        CreatedBy = Normalize(createdBy, 100, nameof(createdBy))!;
        Reference = Normalize(reference, 50, nameof(reference));
        ReferenceType = Normalize(referenceType, 100, nameof(referenceType));
        ReferenceId = referenceId;
        Reason = Normalize(reason, 500, nameof(reason));
        SourceLineNumber = sourceLineNumber;
    }

    private static bool RequiresPositiveDelta(StockMovementType type) =>
        type is StockMovementType.Receipt or StockMovementType.ReturnIn or
            StockMovementType.OpeningBalance or StockMovementType.PurchaseReceipt or
            StockMovementType.SaleReturn or StockMovementType.TransferIn;

    private static bool RequiresNegativeDelta(StockMovementType type) =>
        type is StockMovementType.Issue or StockMovementType.Sale or StockMovementType.TransferOut;

    private static string? Normalize(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (result.Length > maxLength)
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        return result;
    }
}
