using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Inventory.Core.Entities.Catalog;

namespace Simplia.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> b)
    {
        b.ToTable("PriceLists");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsUnicode(false).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.HasIndex(x => new { x.Name, x.CurrencyCode }).IsUnique();
        b.HasMany(x => x.Items).WithOne(x => x.PriceList)
            .HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
    }
}
