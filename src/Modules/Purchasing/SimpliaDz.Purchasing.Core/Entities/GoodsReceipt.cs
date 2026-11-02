namespace SimpliaDz.Purchasing.Core.Entities;

public sealed record GoodsReceiptLineItem(Guid PurchaseOrderLineId, Guid ProductVariantId,
    int QuantityReceived, decimal UnitCost);

public sealed class GoodsReceipt
{
    private readonly List<GoodsReceiptLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public PurchaseOrder PurchaseOrder { get; private set; } = default!;
    public Guid LocationId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string ReceivedBy { get; private set; } = string.Empty;
    public IReadOnlyCollection<GoodsReceiptLine> Lines => _lines;

    private GoodsReceipt() { }

    public GoodsReceipt(Guid id, Guid purchaseOrderId, Guid locationId, string receivedBy,
        IEnumerable<GoodsReceiptLineItem> lines)
    {
        if (id == Guid.Empty) throw new ArgumentException("Receipt ID is required.", nameof(id));
        if (purchaseOrderId == Guid.Empty)
            throw new ArgumentException("Purchase order is required.", nameof(purchaseOrderId));
        if (locationId == Guid.Empty) throw new ArgumentException("Location is required.", nameof(locationId));
        if (string.IsNullOrWhiteSpace(receivedBy) || receivedBy.Trim().Length > 100)
            throw new ArgumentException("Receiver is required and cannot exceed 100 characters.", nameof(receivedBy));
        ArgumentNullException.ThrowIfNull(lines);

        var items = lines.ToList();
        if (items.Count == 0) throw new ArgumentException("Receipt requires at least one line.", nameof(lines));
        if (items.Any(x => x.PurchaseOrderLineId == Guid.Empty || x.ProductVariantId == Guid.Empty ||
                           x.QuantityReceived <= 0 || x.UnitCost < 0 ||
                           x.UnitCost > 99999999999999.9999m || decimal.Round(x.UnitCost, 4) != x.UnitCost))
            throw new ArgumentException("Receipt lines contain invalid values.", nameof(lines));
        if (items.Select(x => x.PurchaseOrderLineId).Distinct().Count() != items.Count)
            throw new ArgumentException("A purchase order line can appear only once per receipt.", nameof(lines));

        Id = id;
        PurchaseOrderId = purchaseOrderId;
        LocationId = locationId;
        ReceivedBy = receivedBy.Trim();
        _lines.AddRange(items.Select(x => new GoodsReceiptLine(id, x)));
    }
}

public sealed class GoodsReceiptLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GoodsReceiptId { get; private set; }
    public GoodsReceipt GoodsReceipt { get; private set; } = default!;
    public Guid PurchaseOrderLineId { get; private set; }
    public PurchaseOrderLine PurchaseOrderLine { get; private set; } = default!;
    public Guid ProductVariantId { get; private set; }
    public int QuantityReceived { get; private set; }
    public decimal UnitCost { get; private set; }

    private GoodsReceiptLine() { }

    internal GoodsReceiptLine(Guid receiptId, GoodsReceiptLineItem item)
    {
        GoodsReceiptId = receiptId;
        PurchaseOrderLineId = item.PurchaseOrderLineId;
        ProductVariantId = item.ProductVariantId;
        QuantityReceived = item.QuantityReceived;
        UnitCost = item.UnitCost;
    }
}
