namespace SimpliaDz.Purchasing.Core.Entities;

public enum PurchaseOrderStatus
{
    Draft = 1,
    Ordered = 2,
    PartiallyReceived = 3,
    Received = 4,
    Cancelled = 5
}

public sealed class PurchaseOrder
{
    private readonly List<PurchaseOrderLine> _lines = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string PurchaseOrderNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = default!;
    public Guid DestinationLocationId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public DateTimeOffset OrderDate { get; private set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PurchaseOrderLine> Lines => _lines;
    public decimal Total => _lines.Sum(line => line.LineTotal);

    private PurchaseOrder() { }

    public PurchaseOrder(Guid supplierId, Guid destinationLocationId, string createdBy,
        string? notes = null)
    {
        if (supplierId == Guid.Empty) throw new ArgumentException("Supplier is required.", nameof(supplierId));
        if (destinationLocationId == Guid.Empty)
            throw new ArgumentException("Destination location is required.", nameof(destinationLocationId));
        if (string.IsNullOrWhiteSpace(createdBy) || createdBy.Trim().Length > 100)
            throw new ArgumentException("Creator is required and cannot exceed 100 characters.", nameof(createdBy));

        SupplierId = supplierId;
        DestinationLocationId = destinationLocationId;
        CreatedBy = createdBy.Trim();
        Notes = NormalizeNotes(notes);
        PurchaseOrderNumber = $"PO-{Id:N}".ToUpperInvariant();
    }

    public PurchaseOrderLine AddLine(Guid productVariantId, int orderedQuantity, decimal unitCost)
    {
        EnsureDraft();
        if (productVariantId == Guid.Empty)
            throw new ArgumentException("Product variant is required.", nameof(productVariantId));
        if (_lines.Any(line => line.ProductVariantId == productVariantId))
            throw new InvalidOperationException("A variant can appear only once in a purchase order.");

        var line = new PurchaseOrderLine(Id, productVariantId, orderedQuantity, unitCost);
        _lines.Add(line);
        Touch();
        return line;
    }

    public void UpdateNotes(string? notes)
    {
        EnsureDraft();
        Notes = NormalizeNotes(notes);
        Touch();
    }

    public void PlaceOrder()
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new InvalidOperationException("A purchase order needs at least one line.");
        Status = PurchaseOrderStatus.Ordered;
        Touch();
    }

    // Called by goods receipt processing in the same transaction as inventory changes.
    public void RecordReceipt(Guid lineId, int quantity)
    {
        if (Status is not (PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived))
            throw new InvalidOperationException("Only an ordered purchase order can receive stock.");
        var line = _lines.FirstOrDefault(x => x.Id == lineId)
            ?? throw new ArgumentException("Line does not belong to this purchase order.", nameof(lineId));
        line.Receive(quantity);
        Status = _lines.All(x => x.ReceivedQuantity == x.OrderedQuantity)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;
        Touch();
    }

    public void Cancel()
    {
        if (Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            throw new InvalidOperationException("Only an unreceived purchase order can be cancelled.");
        Status = PurchaseOrderStatus.Cancelled;
        Touch();
    }

    private void EnsureDraft()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Only a draft purchase order can be edited.");
    }

    private void Touch()
    {
        Revision = checked(Revision + 1);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var normalized = notes.Trim();
        if (normalized.Length > 2000)
            throw new ArgumentException("Notes cannot exceed 2000 characters.", nameof(notes));
        return normalized;
    }
}

public sealed class PurchaseOrderLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; private set; }
    public PurchaseOrder PurchaseOrder { get; private set; } = default!;
    public Guid ProductVariantId { get; private set; }
    public int OrderedQuantity { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineTotal => OrderedQuantity * UnitCost;

    private PurchaseOrderLine() { }

    internal PurchaseOrderLine(Guid purchaseOrderId, Guid productVariantId,
        int orderedQuantity, decimal unitCost)
    {
        if (orderedQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderedQuantity), "Ordered quantity must be positive.");
        if (unitCost < 0)
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Unit cost cannot be negative.");
        if (unitCost > 99999999999999.9999m || decimal.Round(unitCost, 4) != unitCost)
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Unit cost must fit decimal(18,4).");

        PurchaseOrderId = purchaseOrderId;
        ProductVariantId = productVariantId;
        OrderedQuantity = orderedQuantity;
        UnitCost = unitCost;
    }

    internal void Receive(int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (quantity > OrderedQuantity - ReceivedQuantity)
            throw new InvalidOperationException("Receipt exceeds the remaining ordered quantity.");
        ReceivedQuantity = checked(ReceivedQuantity + quantity);
    }
}
