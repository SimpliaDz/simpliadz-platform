namespace SimpliaDz.Sales.Core.Entities;

public sealed record SaleReturnLineItem(Guid OriginalSaleLineId, Guid ProductVariantId,
    int Quantity, decimal UnitRefundAmount, bool Restock);

public sealed class SaleReturn
{
    private readonly List<SaleReturnLine> _lines = [];

    public Guid Id { get; private set; }
    public string ReturnNumber { get; private set; } = string.Empty;
    public Guid OriginalSaleId { get; private set; }
    public Sale OriginalSale { get; private set; } = default!;
    public Guid LocationId { get; private set; }
    public DateTimeOffset ReturnDate { get; private set; } = DateTimeOffset.UtcNow;
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedBy { get; private set; }
    public IReadOnlyCollection<SaleReturnLine> Lines => _lines;
    public decimal RefundTotal => _lines.Sum(x => x.RefundAmount);

    private SaleReturn() { }

    public SaleReturn(Guid id, Guid originalSaleId, Guid locationId, string reason,
        Guid createdBy, IEnumerable<SaleReturnLineItem> lines)
    {
        if (id == Guid.Empty) throw new ArgumentException("Return ID is required.", nameof(id));
        if (originalSaleId == Guid.Empty)
            throw new ArgumentException("Original sale is required.", nameof(originalSaleId));
        if (locationId == Guid.Empty) throw new ArgumentException("Location is required.", nameof(locationId));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("Reason is required and cannot exceed 500 characters.", nameof(reason));
        if (createdBy == Guid.Empty) throw new ArgumentException("Creator is required.", nameof(createdBy));
        ArgumentNullException.ThrowIfNull(lines);
        var items = lines.ToList();
        if (items.Count == 0) throw new ArgumentException("Return needs at least one line.", nameof(lines));
        if (items.Select(x => x.OriginalSaleLineId).Distinct().Count() != items.Count)
            throw new ArgumentException("Original sale line can appear only once per return.", nameof(lines));
        if (items.Any(x => x.OriginalSaleLineId == Guid.Empty || x.ProductVariantId == Guid.Empty ||
                           x.Quantity <= 0 || x.UnitRefundAmount < 0 ||
                           x.UnitRefundAmount > 99999999999999.9999m ||
                           decimal.Round(x.UnitRefundAmount, 4) != x.UnitRefundAmount))
            throw new ArgumentException("Return lines contain invalid values.", nameof(lines));
        Id = id;
        ReturnNumber = $"RET-{id:N}".ToUpperInvariant();
        OriginalSaleId = originalSaleId;
        LocationId = locationId;
        Reason = reason.Trim();
        CreatedBy = createdBy;
        _lines.AddRange(items.Select(x => new SaleReturnLine(id, x)));
    }
}

public sealed class SaleReturnLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SaleReturnId { get; private set; }
    public SaleReturn SaleReturn { get; private set; } = default!;
    public Guid OriginalSaleLineId { get; private set; }
    public SaleLine OriginalSaleLine { get; private set; } = default!;
    public Guid ProductVariantId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitRefundAmount { get; private set; }
    public decimal RefundAmount { get; private set; }
    public bool Restock { get; private set; }

    private SaleReturnLine() { }

    internal SaleReturnLine(Guid saleReturnId, SaleReturnLineItem item)
    {
        if (item.UnitRefundAmount > 99999999999999.9999m / item.Quantity)
            throw new ArgumentOutOfRangeException(nameof(item), "Refund exceeds decimal(18,4).");
        var amount = item.UnitRefundAmount * item.Quantity;
        SaleReturnId = saleReturnId;
        OriginalSaleLineId = item.OriginalSaleLineId;
        ProductVariantId = item.ProductVariantId;
        Quantity = item.Quantity;
        UnitRefundAmount = item.UnitRefundAmount;
        RefundAmount = amount;
        Restock = item.Restock;
    }
}
