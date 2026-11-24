using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> b)
    {
        b.ToTable("Categories");

        b.HasKey(x => x.Id);

        b.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        b.HasOne(x => x.ParentCategory)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Attributes).WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ParentCategoryId, x.Name })
            .IsUnique()
            .HasFilter(null);
        b.HasIndex(x => x.ParentCategoryId);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        b.HasIndex(x => x.IsActive);
    }
}
