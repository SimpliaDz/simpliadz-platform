namespace SimpliaDz.Sales.Core.Entities;

public enum PromotionTarget { Category, Brand, Product, Variant }

public sealed class SalePromotion
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public PromotionTarget Target { get; private set; }
    public Guid TargetId { get; private set; }
    public SaleDiscountType DiscountType { get; private set; }
    public decimal Value { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public bool IsActive { get; private set; }

    public void Configure(string name, PromotionTarget target, Guid targetId, SaleDiscountType discountType,
        decimal value, DateTimeOffset startsAt, DateTimeOffset? endsAt, bool active)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            throw new ArgumentException("Enter a promotion name of up to 120 characters.");
        if (!Enum.IsDefined(target) || targetId == Guid.Empty || !Enum.IsDefined(discountType))
            throw new ArgumentException("Choose a valid target and discount type.");
        if (value <= 0 || value > 99999999999999.9999m || decimal.Round(value, 4) != value ||
            (discountType == SaleDiscountType.Percentage && value > 100m))
            throw new ArgumentException("Enter a valid positive discount (percentages cannot exceed 100).");
        if (endsAt <= startsAt) throw new ArgumentException("The end must follow the start.");
        Name = name.Trim();
        Target = target;
        TargetId = targetId;
        DiscountType = discountType;
        Value = value;
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt?.ToUniversalTime();
        IsActive = active;
    }

    public decimal UnitDiscount(decimal price) => DiscountType == SaleDiscountType.FixedAmount
        ? Math.Min(price, Value)
        : decimal.Round(price * Value / 100m, 4, MidpointRounding.AwayFromZero);
}
