namespace Simplia.Crm.Core.Entities;

public sealed class LoyaltyReward
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public int PointsCost { get; private set; }
    public bool IsActive { get; private set; } = true;

    private LoyaltyReward() { }
    public LoyaltyReward(string name, int pointsCost) => Update(name, pointsCost);

    public void Update(string name, int pointsCost)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            throw new ArgumentException("Reward name is required and must fit 120 characters.", nameof(name));
        if (pointsCost <= 0) throw new ArgumentOutOfRangeException(nameof(pointsCost));
        Name = name.Trim();
        PointsCost = pointsCost;
    }

    public void SetActive(bool active) => IsActive = active;
}
