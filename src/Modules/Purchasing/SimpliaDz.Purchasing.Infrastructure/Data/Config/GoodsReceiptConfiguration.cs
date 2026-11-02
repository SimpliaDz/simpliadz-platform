using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Purchasing.Core.Entities;

namespace SimpliaDz.Purchasing.Infrastructure.Data.Config;

internal sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> b)
    {
        b.ToTable("GoodsReceipts");
        b.HasKey(x => x.Id);
        b.Property(x => x.ReceivedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ReceivedAt).IsRequired();
        b.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.GoodsReceipt)
            .HasForeignKey(x => x.GoodsReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.PurchaseOrderId, x.ReceivedAt });
        b.HasIndex(x => x.LocationId);
    }
}

internal sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> b)
    {
        b.ToTable("GoodsReceiptLines", table =>
        {
            table.HasCheckConstraint("CK_GoodsReceiptLines_QuantityReceived_Positive", "[QuantityReceived] > 0");
            table.HasCheckConstraint("CK_GoodsReceiptLines_UnitCost_NonNegative", "[UnitCost] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.UnitCost).HasPrecision(18, 4).IsRequired();
        b.HasOne(x => x.PurchaseOrderLine).WithMany().HasForeignKey(x => x.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.GoodsReceiptId, x.PurchaseOrderLineId }).IsUnique();
        b.HasIndex(x => x.ProductVariantId);
    }
}
