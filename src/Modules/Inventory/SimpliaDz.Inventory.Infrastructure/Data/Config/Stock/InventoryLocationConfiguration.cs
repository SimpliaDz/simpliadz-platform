using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class InventoryLocationConfiguration : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> b)
    {
        b.ToTable("Locations");

        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.IsActive).IsRequired();

        //indexes    
        b.HasIndex(x => x.Name).IsUnique();
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        b.HasIndex(x => x.IsActive);
    }
}
