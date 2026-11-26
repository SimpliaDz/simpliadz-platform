using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Inventory.Core.Entities.Catalog;

namespace Simplia.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class VariantAttributeValueConfiguration : IEntityTypeConfiguration<VariantAttributeValue>
{
    public void Configure(EntityTypeBuilder<VariantAttributeValue> b)
    {
        b.ToTable("VariantAttributeValues");
        b.HasKey(x => new { x.VariantId, x.AttributeValueId });
        b.HasOne(x => x.AttributeValue).WithMany().HasForeignKey(x => x.AttributeValueId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AttributeValueId);
    }
}
