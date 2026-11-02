using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("StockMovements", table =>
            table.HasCheckConstraint("CK_StockMovements_QuantityDelta_Valid",
                "([Type] = 7 AND [QuantityDelta] >= 0) OR ([Type] <> 7 AND [QuantityDelta] <> 0)"));

        b.HasKey(x => x.Id);

        b.Property(x => x.OccurredAt)
            .IsRequired();

        b.Property(x => x.QuantityDelta)
            .IsRequired();

        // Preserve the historical column names and enum values.
        b.Property(x => x.MovementType).HasColumnName("Type").IsRequired();

        b.Property(x => x.Reference)
            .HasMaxLength(50);

        b.Property(x => x.Reason).HasColumnName("Note").HasMaxLength(500);

        b.Property(x => x.ReferenceType).HasColumnName("SourceDocumentType").HasMaxLength(100);
        b.Property(x => x.ReferenceId).HasColumnName("SourceDocumentId");
        b.Property(x => x.SourceLineNumber);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();


        b.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Useful indexes for history queries
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.VariantId, x.LocationId, x.OccurredAt });
        b.HasIndex(x => new { x.VariantId, x.LocationId })
            .IsUnique()
            .HasDatabaseName("IX_StockMovements_OpeningBalance_Once")
            .HasFilter("[Type] = 7");
        b.HasIndex(x => x.MovementType).HasDatabaseName("IX_StockMovements_Type");
        b.HasIndex(x => x.Reference).HasFilter("[Reference] IS NOT NULL");
        b.HasIndex(x => new { x.ReferenceType, x.ReferenceId })
            .HasDatabaseName("IX_StockMovements_SourceDocumentType_SourceDocumentId")
            .HasFilter("[SourceDocumentType] IS NOT NULL AND [SourceDocumentId] IS NOT NULL");
    }
}
