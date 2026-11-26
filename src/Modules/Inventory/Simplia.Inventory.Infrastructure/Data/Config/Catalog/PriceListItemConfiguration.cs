using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Inventory.Core.Entities.Catalog;

namespace Simplia.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class PriceListItemConfiguration : IEntityTypeConfiguration<PriceListItem>
{
    public void Configure(EntityTypeBuilder<PriceListItem> b)
    {
        b.ToTable("PriceListItems", table => table.HasCheckConstraint("CK_PriceListItems_Price", "[Price] >= 0"));
        b.HasKey(x => new { x.PriceListId, x.ProductVariantId });
        b.Property(x => x.Price).HasColumnType("decimal(18,2)").IsRequired();
        b.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ProductVariantId);
    }
}
