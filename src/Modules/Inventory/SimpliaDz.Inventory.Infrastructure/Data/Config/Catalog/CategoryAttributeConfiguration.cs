using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class CategoryAttributeConfiguration : IEntityTypeConfiguration<CategoryAttribute>
{
    public void Configure(EntityTypeBuilder<CategoryAttribute> b)
    {
        b.ToTable("CategoryAttributes");
        b.HasKey(x => new { x.CategoryId, x.AttributeId });
        b.HasOne(x => x.Category).WithMany(x => x.Attributes).HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Attribute).WithMany().HasForeignKey(x => x.AttributeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
