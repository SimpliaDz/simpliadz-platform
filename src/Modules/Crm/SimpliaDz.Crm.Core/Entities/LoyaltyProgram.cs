namespace SimpliaDz.Crm.Core.Entities;

public sealed class LoyaltyProgram
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public int ScopeKey { get; private set; } = 1;
    public string Name { get; private set; } = string.Empty;
    public decimal SpendAmountPerPoint { get; private set; }
    public string CurrencyCode { get; private set; } = "DZD";
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private LoyaltyProgram() { }

    public LoyaltyProgram(string name, decimal spendAmountPerPoint) =>
        Update(name, spendAmountPerPoint);

    public void Update(string name, decimal spendAmountPerPoint)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            throw new ArgumentException("Program name is required and must fit 120 characters.", nameof(name));
        if (spendAmountPerPoint <= 0 || spendAmountPerPoint > 99999999999999.9999m ||
            decimal.Round(spendAmountPerPoint, 4) != spendAmountPerPoint)
            throw new ArgumentOutOfRangeException(nameof(spendAmountPerPoint));
        Name = name.Trim();
        SpendAmountPerPoint = spendAmountPerPoint;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
