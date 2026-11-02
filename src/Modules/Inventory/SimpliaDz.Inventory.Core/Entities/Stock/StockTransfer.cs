using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Core.Entities.Stock;

public enum StockTransferStatus
{
    Draft = 1,
    Completed = 2,
    Cancelled = 3
}

public sealed class StockTransfer
{
    private readonly List<StockTransferLine> _lines = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string TransferNumber { get; private set; } = string.Empty;
    public Guid SourceLocationId { get; private set; }
    public InventoryLocation SourceLocation { get; private set; } = default!;
    public Guid DestinationLocationId { get; private set; }
    public InventoryLocation DestinationLocation { get; private set; } = default!;
    public StockTransferStatus Status { get; private set; } = StockTransferStatus.Draft;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public int Revision { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<StockTransferLine> Lines => _lines;

    private StockTransfer() { }

    public StockTransfer(Guid sourceLocationId, Guid destinationLocationId, string createdBy)
    {
        if (sourceLocationId == Guid.Empty) throw new ArgumentException("Source location is required.", nameof(sourceLocationId));
        if (destinationLocationId == Guid.Empty) throw new ArgumentException("Destination location is required.", nameof(destinationLocationId));
        if (sourceLocationId == destinationLocationId)
            throw new ArgumentException("Source and destination must differ.", nameof(destinationLocationId));
        if (string.IsNullOrWhiteSpace(createdBy) || createdBy.Trim().Length > 100)
            throw new ArgumentException("Creator is required and cannot exceed 100 characters.", nameof(createdBy));

        SourceLocationId = sourceLocationId;
        DestinationLocationId = destinationLocationId;
        CreatedBy = createdBy.Trim();
        TransferNumber = $"TR-{Id:N}".ToUpperInvariant();
    }

    public StockTransferLine AddLine(Guid variantId, int quantity)
    {
        EnsureDraft();
        if (variantId == Guid.Empty) throw new ArgumentException("Variant is required.", nameof(variantId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Transfer quantity must be positive.");
        if (_lines.Any(line => line.ProductVariantId == variantId))
            throw new InvalidOperationException("A variant can appear only once in a transfer.");

        var line = new StockTransferLine(Id, variantId, quantity);
        _lines.Add(line);
        Revision = checked(Revision + 1);
        UpdatedAt = DateTimeOffset.UtcNow;
        return line;
    }

    public void Complete(string completedBy)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new InvalidOperationException("A transfer needs at least one line.");
        if (string.IsNullOrWhiteSpace(completedBy) || completedBy.Trim().Length > 100)
            throw new ArgumentException("Completing user is required and cannot exceed 100 characters.", nameof(completedBy));

        Status = StockTransferStatus.Completed;
        CompletedBy = completedBy.Trim();
        CompletedAt = DateTimeOffset.UtcNow;
        Revision = checked(Revision + 1);
        UpdatedAt = CompletedAt.Value;
    }

    public void Cancel()
    {
        EnsureDraft();
        Status = StockTransferStatus.Cancelled;
        Revision = checked(Revision + 1);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureDraft()
    {
        if (Status != StockTransferStatus.Draft)
            throw new InvalidOperationException("Only draft transfers can be changed.");
    }
}

public sealed class StockTransferLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid StockTransferId { get; private set; }
    public StockTransfer StockTransfer { get; private set; } = default!;
    public Guid ProductVariantId { get; private set; }
    public ProductVariant ProductVariant { get; private set; } = default!;
    public int Quantity { get; private set; }

    private StockTransferLine() { }

    internal StockTransferLine(Guid stockTransferId, Guid productVariantId, int quantity)
    {
        StockTransferId = stockTransferId;
        ProductVariantId = productVariantId;
        Quantity = quantity;
    }
}
