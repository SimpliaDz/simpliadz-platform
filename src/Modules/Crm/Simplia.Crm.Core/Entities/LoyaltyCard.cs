namespace Simplia.Crm.Core.Entities;

public sealed class LoyaltyCard
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public string CardNumber { get; private set; } = string.Empty;
    public int PointsBalance { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; private set; } = [];

    private LoyaltyCard() { }

    public LoyaltyCard(Guid customerId)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer is required.", nameof(customerId));
        CustomerId = customerId;
        CardNumber = $"LDZ{Id:N}".ToUpperInvariant();
    }

    public void SetActive(bool active) => IsActive = active;

    public void ApplyPoints(int delta)
    {
        PointsBalance = checked(PointsBalance + delta);
    }
}
