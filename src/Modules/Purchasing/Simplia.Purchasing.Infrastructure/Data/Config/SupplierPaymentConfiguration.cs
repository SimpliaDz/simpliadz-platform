using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Purchasing.Core.Entities;

namespace Simplia.Purchasing.Infrastructure.Data.Config;

internal sealed class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> b)
    {
        b.ToTable("SupplierPayments", t =>
            t.HasCheckConstraint("CK_SupplierPayments_PositiveAmount", "[Amount] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 4);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PurchaseOrderId, x.PaymentDate });
    }
}
