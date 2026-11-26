using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Inventory.Core.Entities.Stock;

namespace Simplia.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class InventoryPolicyConfiguration : IEntityTypeConfiguration<InventoryPolicy>
{
    public void Configure(EntityTypeBuilder<InventoryPolicy> b)
    {
        b.ToTable("InventoryPolicies", table =>
        {
            table.HasCheckConstraint("CK_InventoryPolicies_ReorderPoint_NonNegative",
                "[ReorderPoint] >= 0");
            table.HasCheckConstraint("CK_InventoryPolicies_TargetStockLevel_Valid",
                "[TargetStockLevel] IS NULL OR [TargetStockLevel] >= [ReorderPoint]");
        });
        b.HasKey(x => new { x.ProductVariantId, x.LocationId });
        b.HasOne(x => x.ProductVariant).WithMany()
            .HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Location).WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ReorderPoint).IsRequired();
        b.Property(x => x.TargetStockLevel);
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.LocationId);
    }
}
