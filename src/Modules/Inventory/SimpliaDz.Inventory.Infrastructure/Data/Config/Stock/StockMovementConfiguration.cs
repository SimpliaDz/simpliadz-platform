using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Stock;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("StockMovements");

        b.HasKey(x => x.Id);

        b.Property(x => x.OccurredAt)
            .IsRequired();

        b.Property(x => x.QuantityDelta)
            .IsRequired();

        // Store enum as int (default)
        b.Property(x => x.Type)
            .IsRequired();

        b.Property(x => x.Reference)
            .HasMaxLength(50);

        b.Property(x => x.Note)
            .HasMaxLength(500);

        b.Property(x => x.SourceDocumentType).HasMaxLength(100);
        b.Property(x => x.SourceDocumentId);
        b.Property(x => x.SourceLineNumber);


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
        b.HasIndex(x => x.Type);
        b.HasIndex(x => x.Reference).HasFilter("[Reference] IS NOT NULL");
        b.HasIndex(x => new { x.SourceDocumentType, x.SourceDocumentId })
            .HasFilter("[SourceDocumentType] IS NOT NULL AND [SourceDocumentId] IS NOT NULL");
    }
}
