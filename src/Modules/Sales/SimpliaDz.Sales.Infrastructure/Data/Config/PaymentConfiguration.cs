using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Sales.Core.Entities;

namespace SimpliaDz.Sales.Infrastructure.Data.Config;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments", table =>
            table.HasCheckConstraint("CK_Payments_Amount_Positive", "[Amount] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.PaidAt).IsRequired();
        b.Property(x => x.ReceivedBy).IsRequired();
        b.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SaleId, x.PaidAt });
    }
}
