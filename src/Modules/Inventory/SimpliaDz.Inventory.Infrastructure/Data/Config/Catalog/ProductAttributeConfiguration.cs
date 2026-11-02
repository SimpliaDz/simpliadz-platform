using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> b)
    {
        b.ToTable("ProductAttributes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasMany(x => x.Values).WithOne(x => x.Attribute)
            .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
    }
}
