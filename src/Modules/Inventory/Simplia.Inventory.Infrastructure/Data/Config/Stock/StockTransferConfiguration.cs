using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Inventory.Core.Entities.Stock;

namespace Simplia.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> b)
    {
        b.ToTable("StockTransfers", table =>
            table.HasCheckConstraint("CK_StockTransfers_DistinctLocations",
                "[SourceLocationId] <> [DestinationLocationId]"));
        b.HasKey(x => x.Id);
        b.Property(x => x.TransferNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.TransferNumber).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.Revision).IsRequired();
        b.Property(x => x.CompletedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.SourceLocation).WithMany()
            .HasForeignKey(x => x.SourceLocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DestinationLocation).WithMany()
            .HasForeignKey(x => x.DestinationLocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.StockTransfer)
            .HasForeignKey(x => x.StockTransferId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

internal sealed class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> b)
    {
        b.ToTable("StockTransferLines", table =>
            table.HasCheckConstraint("CK_StockTransferLines_Quantity_Positive", "[Quantity] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).IsRequired();
        b.HasIndex(x => new { x.StockTransferId, x.ProductVariantId }).IsUnique();
        b.HasOne(x => x.ProductVariant).WithMany()
            .HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
    }
}
