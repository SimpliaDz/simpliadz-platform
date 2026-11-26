namespace Simplia.Inventory.Core.Entities.Stock;

public sealed class InventoryWarningSettings
{
    public static readonly Guid SingletonId = Guid.Parse("20c7176d-2d2f-4d20-99f0-a99ca2874d40");
    public Guid Id { get; private set; } = SingletonId;
    public bool Enabled { get; private set; }
    public bool LowStockEnabled { get; private set; } = true;
    public bool AgeingEnabled { get; private set; } = true;
    public int IntervalMinutes { get; private set; } = 60;
    public int AgeThresholdDays { get; private set; } = 90;
    public DateTimeOffset? LastRunAt { get; private set; }
    public DateTimeOffset NextRunAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }

    private InventoryWarningSettings() { }

    public void Configure(bool enabled, bool lowStockEnabled, bool ageingEnabled,
        int intervalMinutes, int ageThresholdDays)
    {
        if (intervalMinutes is < 5 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(intervalMinutes));
        if (ageThresholdDays is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(ageThresholdDays));
        Enabled = enabled;
        LowStockEnabled = lowStockEnabled;
        AgeingEnabled = ageingEnabled;
        IntervalMinutes = intervalMinutes;
        AgeThresholdDays = ageThresholdDays;
        NextRunAt = DateTimeOffset.UtcNow;
    }
}
