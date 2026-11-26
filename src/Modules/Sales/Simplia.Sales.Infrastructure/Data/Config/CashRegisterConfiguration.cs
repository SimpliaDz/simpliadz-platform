using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Sales.Core.Entities;

namespace Simplia.Sales.Infrastructure.Data.Config;

internal sealed class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> b)
    {
        b.ToTable("CashRegisters");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasIndex(x => new { x.LocationId, x.Code }).IsUnique();
        b.HasIndex(x => x.LocationId);
    }
}
