using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class InventoryWarningSettingsConfiguration : IEntityTypeConfiguration<InventoryWarningSettings>
{
    public void Configure(EntityTypeBuilder<InventoryWarningSettings> b)
    {
        b.ToTable("InventoryWarningSettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasData(new
        {
            Id = InventoryWarningSettings.SingletonId,
            Enabled = false, LowStockEnabled = true, AgeingEnabled = true,
            IntervalMinutes = 60, AgeThresholdDays = 90,
            NextRunAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero)
        });
    }
}
