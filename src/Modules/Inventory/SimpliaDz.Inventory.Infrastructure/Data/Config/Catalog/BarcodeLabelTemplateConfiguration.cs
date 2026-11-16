using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class BarcodeLabelTemplateConfiguration : IEntityTypeConfiguration<BarcodeLabelTemplate>
{
    public void Configure(EntityTypeBuilder<BarcodeLabelTemplate> b)
    {
        b.ToTable("BarcodeLabelTemplates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.PageWidthMm).HasPrecision(8, 2);
        b.Property(x => x.PageHeightMm).HasPrecision(8, 2);
        b.Property(x => x.LabelWidthMm).HasPrecision(8, 2);
        b.Property(x => x.LabelHeightMm).HasPrecision(8, 2);
        b.Property(x => x.MarginMm).HasPrecision(8, 2);
        b.Property(x => x.GapMm).HasPrecision(8, 2);
        b.HasData(new
        {
            Id = Guid.Parse("5369ed28-f57a-49be-a587-13f4d0d091f1"),
            Name = "Standard A4 90 x 40 mm",
            PageWidthMm = 210m, PageHeightMm = 297m,
            LabelWidthMm = 90m, LabelHeightMm = 40m,
            MarginMm = 5m, GapMm = 3m,
            ShowName = true, ShowSku = true, IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero)
        });
    }
}
