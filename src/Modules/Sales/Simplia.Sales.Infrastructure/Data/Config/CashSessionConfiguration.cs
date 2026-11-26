using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Sales.Core.Entities;

namespace Simplia.Sales.Infrastructure.Data.Config;

internal sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> b)
    {
        b.ToTable("CashSessions", table =>
        {
            table.HasCheckConstraint("CK_CashSessions_OpeningCash", "[OpeningCash] >= 0");
            table.HasCheckConstraint("CK_CashSessions_ClosingCash",
                "([Status] = 'Open' AND [ClosedAt] IS NULL AND [CountedCash] IS NULL AND [ExpectedCash] IS NULL) OR " +
                "([Status] = 'Closed' AND [ClosedAt] IS NOT NULL AND [CountedCash] >= 0 AND [ExpectedCash] >= 0)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.OpeningCash).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.ExpectedCash).HasPrecision(18, 4);
        b.Property(x => x.CountedCash).HasPrecision(18, 4);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Ignore(x => x.Difference);
        b.HasOne(x => x.CashRegister).WithMany().HasForeignKey(x => x.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CashRegisterId).IsUnique().HasFilter("[Status] = 'Open'");
        b.HasIndex(x => x.OpenedAt);
    }
}
