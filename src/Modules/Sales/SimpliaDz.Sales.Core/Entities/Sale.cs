namespace SimpliaDz.Sales.Core.Entities;

public enum SaleStatus
{
    Draft = 1,
    Completed = 2,
    Cancelled = 3,
    PartiallyReturned = 4,
    Returned = 5
}

public sealed class Sale
{
    private const decimal MaxMoney = 99999999999999.9999m;
    private readonly List<SaleLine> _lines = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string? SaleNumber { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public Guid CashierId { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public Guid PriceListId { get; private set; }
    public SaleStatus Status { get; private set; } = SaleStatus.Draft;
    public DateTimeOffset SaleDate { get; private set; } = DateTimeOffset.UtcNow;
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal Total { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<SaleLine> Lines => _lines;

    private Sale() { }

    public Sale(Guid locationId, Guid? customerId, Guid cashierId, Guid priceListId)
    {
        if (locationId == Guid.Empty) throw new ArgumentException("Location is required.", nameof(locationId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID must be valid.", nameof(customerId));
        if (cashierId == Guid.Empty) throw new ArgumentException("Cashier is required.", nameof(cashierId));
        if (priceListId == Guid.Empty) throw new ArgumentException("Price list is required.", nameof(priceListId));
        LocationId = locationId;
        CustomerId = customerId;
        CashierId = cashierId;
        PriceListId = priceListId;
    }

    public SaleLine AddLine(Guid productVariantId, int quantity, decimal unitPrice,
        decimal unitCostSnapshot, decimal discountAmount = 0)
    {
        EnsureDraft();
        if (productVariantId == Guid.Empty)
            throw new ArgumentException("Product variant is required.", nameof(productVariantId));
        if (_lines.Any(x => x.ProductVariantId == productVariantId))
            throw new InvalidOperationException("Variant already exists in this sale.");
        var line = new SaleLine(Id, productVariantId, quantity, unitPrice,
            unitCostSnapshot, discountAmount);
        if (line.GrossAmount > MaxMoney - Subtotal || line.LineTotal > MaxMoney - Total)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sale total exceeds decimal(18,4).");
        _lines.Add(line);
        Subtotal += line.GrossAmount;
        Total += line.LineTotal;
        Revision = checked(Revision + 1);
        return line;
    }

    public void SetDiscount(decimal discountAmount)
    {
        EnsureDraft();
        if (discountAmount < 0 || discountAmount > MaxMoney ||
            decimal.Round(discountAmount, 4) != discountAmount)
            throw new ArgumentOutOfRangeException(nameof(discountAmount));
        var afterLineDiscounts = _lines.Sum(x => x.LineTotal);
        if (discountAmount > afterLineDiscounts)
            throw new ArgumentException("Sale discount cannot exceed the line total.", nameof(discountAmount));
        DiscountAmount = discountAmount;
        Total = afterLineDiscounts - discountAmount;
        Revision = checked(Revision + 1);
    }

    public void Complete(Guid completedBy)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new InvalidOperationException("A sale requires at least one line.");
        if (completedBy == Guid.Empty) throw new ArgumentException("Completing user is required.", nameof(completedBy));
        Status = SaleStatus.Completed;
        CompletedBy = completedBy;
        SaleNumber = $"SALE-{Id:N}".ToUpperInvariant();
        SaleDate = DateTimeOffset.UtcNow;
        Revision = checked(Revision + 1);
    }

    public void Cancel()
    {
        EnsureDraft();
        Status = SaleStatus.Cancelled;
        Revision = checked(Revision + 1);
    }

    private void EnsureDraft()
    {
        if (Status != SaleStatus.Draft)
            throw new InvalidOperationException("Only draft sales can be changed.");
    }
}

public sealed class SaleLine
{
    private const decimal MaxMoney = 99999999999999.9999m;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SaleId { get; private set; }
    public Sale Sale { get; private set; } = default!;
    public Guid ProductVariantId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal UnitCostSnapshot { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public decimal GrossAmount => Quantity * UnitPrice;

    private SaleLine() { }

    internal SaleLine(Guid saleId, Guid productVariantId, int quantity, decimal unitPrice,
        decimal unitCostSnapshot, decimal discountAmount)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPrice < 0 || unitPrice > 9999999999999999.99m ||
            decimal.Round(unitPrice, 2) != unitPrice)
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (unitCostSnapshot < 0 || unitCostSnapshot > MaxMoney ||
            decimal.Round(unitCostSnapshot, 4) != unitCostSnapshot)
            throw new ArgumentOutOfRangeException(nameof(unitCostSnapshot));
        if (discountAmount < 0 || discountAmount > MaxMoney ||
            decimal.Round(discountAmount, 4) != discountAmount)
            throw new ArgumentOutOfRangeException(nameof(discountAmount));
        if (unitPrice > 0 && quantity > MaxMoney / unitPrice)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Line total exceeds decimal(18,4).");
        var gross = quantity * unitPrice;
        if (discountAmount > gross)
            throw new ArgumentException("Line total must fit decimal(18,4) and discount cannot exceed gross.");
        SaleId = saleId;
        ProductVariantId = productVariantId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        UnitCostSnapshot = unitCostSnapshot;
        DiscountAmount = discountAmount;
        LineTotal = gross - discountAmount;
    }
}
