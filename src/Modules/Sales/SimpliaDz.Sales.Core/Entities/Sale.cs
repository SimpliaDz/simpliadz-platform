namespace SimpliaDz.Sales.Core.Entities;

public enum SaleStatus
{
    Draft = 1,
    Completed = 2,
    Cancelled = 3,
    PartiallyReturned = 4,
    Returned = 5
}

public enum SaleDiscountType
{
    FixedAmount = 1,
    Percentage = 2
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
    public SaleDiscountType? DiscountType { get; private set; }
    public decimal? DiscountValue { get; private set; }
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
        if (line.GrossAmount > MaxMoney - Subtotal)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sale total exceeds decimal(18,4).");
        _lines.Add(line);
        Subtotal += line.GrossAmount;
        var afterLineDiscounts = _lines.Sum(x => x.LineTotal);
        if (DiscountType == SaleDiscountType.Percentage)
            DiscountAmount = CalculateDiscount(afterLineDiscounts,
                SaleDiscountType.Percentage, DiscountValue!.Value);
        Total = afterLineDiscounts - DiscountAmount;
        Revision = checked(Revision + 1);
        return line;
    }

    public void SetDiscount(decimal discountAmount)
        => ApplySaleDiscount(SaleDiscountType.FixedAmount, discountAmount);

    public void ApplySaleDiscount(SaleDiscountType type, decimal value)
    {
        EnsureDraft();
        var afterLineDiscounts = _lines.Sum(x => x.LineTotal);
        var amount = CalculateDiscount(afterLineDiscounts, type, value);
        DiscountType = type;
        DiscountValue = value;
        DiscountAmount = amount;
        Total = afterLineDiscounts - amount;
        Revision = checked(Revision + 1);
    }

    public void ApplyLineDiscount(Guid lineId, SaleDiscountType type, decimal value)
    {
        EnsureDraft();
        var line = _lines.FirstOrDefault(x => x.Id == lineId)
            ?? throw new ArgumentException("Line does not belong to this sale.", nameof(lineId));
        var amount = CalculateDiscount(line.GrossAmount, type, value);
        var afterLineDiscounts = _lines.Sum(x => x.LineTotal) - line.LineTotal +
            (line.GrossAmount - amount);
        var saleDiscount = DiscountType switch
        {
            SaleDiscountType.Percentage => CalculateDiscount(afterLineDiscounts,
                SaleDiscountType.Percentage, DiscountValue!.Value),
            SaleDiscountType.FixedAmount => DiscountAmount,
            _ => 0m
        };
        if (saleDiscount > afterLineDiscounts)
            throw new ArgumentException("Sale discount would exceed the discounted line total.", nameof(value));
        line.ApplyDiscount(type, value, amount);
        DiscountAmount = saleDiscount;
        Total = afterLineDiscounts - saleDiscount;
        Revision = checked(Revision + 1);
    }

    internal static decimal CalculateDiscount(decimal baseAmount, SaleDiscountType type, decimal value)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (value < 0 || value > MaxMoney || decimal.Round(value, 4) != value)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (type == SaleDiscountType.Percentage && value > 100m)
            throw new ArgumentOutOfRangeException(nameof(value), "Percentage must be between 0 and 100.");
        var amount = type == SaleDiscountType.FixedAmount
            ? value
            : decimal.Round(baseAmount * value / 100m, 4, MidpointRounding.AwayFromZero);
        if (amount > baseAmount)
            throw new ArgumentException("Discount cannot exceed the amount.", nameof(value));
        return amount;
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
    public SaleDiscountType? DiscountType { get; private set; }
    public decimal? DiscountValue { get; private set; }
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
        if (discountAmount > 0)
        {
            DiscountType = SaleDiscountType.FixedAmount;
            DiscountValue = discountAmount;
        }
        LineTotal = gross - discountAmount;
    }

    internal void ApplyDiscount(SaleDiscountType type, decimal value, decimal amount)
    {
        DiscountType = type;
        DiscountValue = value;
        DiscountAmount = amount;
        LineTotal = GrossAmount - amount;
    }
}
