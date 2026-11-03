using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>
{
    public void Configure(EntityTypeBuilder<InventoryCount> b)
    {
        b.ToTable("InventoryCounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.StartedAt).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.CompletedBy).HasMaxLength(100);
        b.Property(x => x.Revision).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.InventoryCount)
            .HasForeignKey(x => x.InventoryCountId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.LocationId, x.StartedAt });
        b.HasIndex(x => x.Status);
    }
}

internal sealed class InventoryCountLineConfiguration : IEntityTypeConfiguration<InventoryCountLine>
{
    public void Configure(EntityTypeBuilder<InventoryCountLine> b)
    {
        b.ToTable("InventoryCountLines", table =>
        {
            table.HasCheckConstraint("CK_InventoryCountLines_ExpectedQuantity_NonNegative",
                "[ExpectedQuantity] >= 0");
            table.HasCheckConstraint("CK_InventoryCountLines_CountedQuantity_NonNegative",
                "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Ignore(x => x.Difference);
        b.Property(x => x.ExpectedQuantity).IsRequired();
        b.HasIndex(x => new { x.InventoryCountId, x.ProductVariantId }).IsUnique();
        b.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
