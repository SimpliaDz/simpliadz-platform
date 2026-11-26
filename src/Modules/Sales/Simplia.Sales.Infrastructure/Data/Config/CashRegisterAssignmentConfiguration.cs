using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Sales.Core.Entities;

namespace Simplia.Sales.Infrastructure.Data.Config;

internal sealed class CashRegisterAssignmentConfiguration : IEntityTypeConfiguration<CashRegisterAssignment>
{
    public void Configure(EntityTypeBuilder<CashRegisterAssignment> b)
    {
        b.ToTable("CashRegisterAssignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.AssignedAt).IsRequired();
        b.HasOne(x => x.CashRegister).WithMany().HasForeignKey(x => x.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CashSession).WithMany().HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CashRegisterId).IsUnique().HasFilter("[ReleasedAt] IS NULL");
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[ReleasedAt] IS NULL");
        b.HasIndex(x => x.CashSessionId);
    }
}
