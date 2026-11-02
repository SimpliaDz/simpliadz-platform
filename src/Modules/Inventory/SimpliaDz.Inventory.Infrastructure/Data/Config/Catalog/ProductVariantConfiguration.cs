using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.ValueObjects;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.ToTable("ProductVariants");

        b.HasKey(x => x.Id);

        // Value Object mapping
        b.Property(x => x.Sku)
            .HasConversion(v => v.Value, v => new Sku(v))
            .HasMaxLength(Sku.MaxLength)
            .IsRequired();

        b.Property(x => x.Barcode).HasMaxLength(64);
        b.Property(x => x.Color).HasMaxLength(64);
        b.Property(x => x.Size).HasMaxLength(32);

        b.Property(x => x.RetailPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        b.Property(x => x.WholesalePrice)
            .HasColumnType("decimal(18,2)");

        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CombinationKey).HasMaxLength(64);
        b.HasMany(x => x.AttributeValues).WithOne(x => x.Variant)
            .HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);

        // Indexes
        b.HasIndex(x => x.ProductId);
        b.HasIndex(x => x.Sku).IsUnique();
        b.HasIndex(x => new { x.ProductId, x.CombinationKey }).IsUnique()
            .HasFilter("[CombinationKey] IS NOT NULL");

        b.HasIndex(x => x.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL");

        b.HasIndex(x => new { x.ProductId, x.Color, x.Size });
    }
}
