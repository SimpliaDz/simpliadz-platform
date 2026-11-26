using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Inventory.Core.Entities.Catalog;

namespace Simplia.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class CategoryAttributeConfiguration : IEntityTypeConfiguration<CategoryAttribute>
{
    public void Configure(EntityTypeBuilder<CategoryAttribute> b)
    {
        b.ToTable("CategoryAttributes");
        b.HasKey(x => new { x.CategoryId, x.AttributeId });
        b.Property(x => x.IsRequired).IsRequired();
        b.HasOne(x => x.Category).WithMany(x => x.Attributes).HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Attribute).WithMany().HasForeignKey(x => x.AttributeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
