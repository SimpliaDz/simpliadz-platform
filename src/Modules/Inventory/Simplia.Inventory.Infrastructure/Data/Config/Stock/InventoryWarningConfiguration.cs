using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.Core.Entities.Stock;

namespace Simplia.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class InventoryWarningConfiguration : IEntityTypeConfiguration<InventoryWarning>
{
    public void Configure(EntityTypeBuilder<InventoryWarning> b)
    {
        b.ToTable("InventoryWarnings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.ProductVariantId, x.LocationId, x.Kind }).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.LocationId, x.Kind });
        b.HasOne<ProductVariant>()
            .WithMany().HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InventoryLocation>().WithMany().HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
