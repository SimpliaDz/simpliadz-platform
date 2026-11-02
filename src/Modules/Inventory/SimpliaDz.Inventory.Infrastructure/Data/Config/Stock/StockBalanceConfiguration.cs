using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> b)
    {
        // Keep the existing table and data; only add a concurrency token.
        b.ToTable("StockItems", t =>
        {
            t.HasCheckConstraint("CK_StockItems_QuantityOnHand_NonNegative", "[QuantityOnHand] >= 0");
            t.HasCheckConstraint("CK_StockItems_ReorderPoint_NonNegative", "[ReorderPoint] IS NULL OR [ReorderPoint] >= 0");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.QuantityOnHand).IsRequired();
        b.Property(x => x.ReorderPoint);
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1 row per (Variant, Location)
        b.HasIndex(x => new { x.VariantId, x.LocationId }).IsUnique();

        b.HasIndex(x => x.LocationId);
        b.HasIndex(x => x.VariantId);
    }
}
