using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.ValueObjects;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");

        b.HasKey(x => x.Id);

        b.Property(x => x.Name)
            .HasConversion(v => v.Value, v => new ProductName(v))
            .HasMaxLength(ProductName.MaxLength)
            .IsRequired();

        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Code).HasMaxLength(50);

        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();

        b.HasOne(x => x.Brand)
            .WithMany()
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ProductCategory)
            .WithMany()
            .HasForeignKey(x => x.ProductCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // ✅ Simple relationship mapping
        b.HasMany(x => x.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        b.HasIndex(x => x.Name).HasDatabaseName("IX_Products_Name_PosLookup");
        b.HasIndex(x => x.BrandId);
        b.HasIndex(x => x.ProductCategoryId);
        b.HasIndex(x => x.IsActive);
    }
}
