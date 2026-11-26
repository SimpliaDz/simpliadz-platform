using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Sales.Core.Entities;

namespace Simplia.Sales.Infrastructure.Data.Config;

internal sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> b)
    {
        b.ToTable("SaleReturns");
        b.HasKey(x => x.Id);
        b.Property(x => x.ReturnNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.ReturnNumber).IsUnique();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.ReturnDate).IsRequired();
        b.Ignore(x => x.RefundTotal);
        b.HasOne(x => x.OriginalSale).WithMany().HasForeignKey(x => x.OriginalSaleId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.SaleReturn)
            .HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.OriginalSaleId, x.ReturnDate });
        b.HasIndex(x => x.LocationId);
    }
}

internal sealed class SaleReturnLineConfiguration : IEntityTypeConfiguration<SaleReturnLine>
{
    public void Configure(EntityTypeBuilder<SaleReturnLine> b)
    {
        b.ToTable("SaleReturnLines", table =>
        {
            table.HasCheckConstraint("CK_SaleReturnLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_SaleReturnLines_Refund_NonNegative",
                "[UnitRefundAmount] >= 0 AND [RefundAmount] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.UnitRefundAmount).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.RefundAmount).HasPrecision(18, 4).IsRequired();
        b.HasOne(x => x.OriginalSaleLine).WithMany().HasForeignKey(x => x.OriginalSaleLineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SaleReturnId, x.OriginalSaleLineId }).IsUnique();
        b.HasIndex(x => x.ProductVariantId);
    }
}
