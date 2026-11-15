using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Sales.Core.Entities;

namespace SimpliaDz.Sales.Infrastructure.Data.Config;

internal sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> b)
    {
        b.ToTable("CashMovements", table =>
            table.HasCheckConstraint("CK_CashMovements_NonZero", "[AmountDelta] <> 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.AmountDelta).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.OccurredAt).IsRequired();
        b.HasOne(x => x.CashSession).WithMany().HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CashSessionId, x.OccurredAt });
    }
}
