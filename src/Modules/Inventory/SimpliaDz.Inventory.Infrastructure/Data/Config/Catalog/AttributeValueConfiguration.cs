using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class AttributeValueConfiguration : IEntityTypeConfiguration<AttributeValue>
{
    public void Configure(EntityTypeBuilder<AttributeValue> b)
    {
        b.ToTable("AttributeValues");
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasMaxLength(150).IsRequired();
        b.Property(x => x.DisplayOrder).IsRequired();
        b.HasIndex(x => new { x.AttributeId, x.Value }).IsUnique();
        b.HasIndex(x => new { x.AttributeId, x.DisplayOrder });
    }
}
