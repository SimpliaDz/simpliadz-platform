using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Purchasing.Core.Entities;

namespace Simplia.Purchasing.Infrastructure.Data.Config;

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable("PurchaseOrders");
        b.HasKey(x => x.Id);
        b.Property(x => x.PurchaseOrderNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.PurchaseOrderNumber).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.OrderDate).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.Revision).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Supplier).WithMany()
            .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.PurchaseOrder)
            .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.SupplierId, x.OrderDate });
        b.HasIndex(x => x.DestinationLocationId);
        b.HasIndex(x => new { x.Status, x.OrderDate });
    }
}

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> b)
    {
        b.ToTable("PurchaseOrderLines", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderLines_OrderedQuantity_Positive", "[OrderedQuantity] > 0");
            table.HasCheckConstraint("CK_PurchaseOrderLines_ReceivedQuantity_Range",
                "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [OrderedQuantity]");
            table.HasCheckConstraint("CK_PurchaseOrderLines_UnitCost_NonNegative", "[UnitCost] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.OrderedQuantity).IsRequired();
        b.Property(x => x.ReceivedQuantity).IsRequired();
        b.Property(x => x.UnitCost).HasPrecision(18, 4).IsRequired();
        b.HasIndex(x => new { x.PurchaseOrderId, x.ProductVariantId }).IsUnique();
        b.HasIndex(x => x.ProductVariantId);
    }
}
