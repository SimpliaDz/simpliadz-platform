using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class InventoryLocationConfiguration : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> b)
    {
        b.ToTable("Locations", table =>
            table.HasCheckConstraint("CK_Locations_Type", "[Type] IN (1, 2)"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Type).HasConversion<int>().IsRequired();
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.IsActive).IsRequired();

        b.HasIndex(x => x.Name);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        b.HasIndex(x => x.IsActive);
    }
}
