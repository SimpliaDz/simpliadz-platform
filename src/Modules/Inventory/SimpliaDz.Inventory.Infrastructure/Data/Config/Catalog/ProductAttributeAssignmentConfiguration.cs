using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class ProductAttributeAssignmentConfiguration : IEntityTypeConfiguration<ProductAttributeAssignment>
{
    public void Configure(EntityTypeBuilder<ProductAttributeAssignment> b)
    {
        b.ToTable("ProductAttributeAssignments");
        b.HasKey(x => new { x.ProductId, x.AttributeId });
        b.HasOne(x => x.Attribute).WithMany().HasForeignKey(x => x.AttributeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
