using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Sales.Core.Entities;

namespace Simplia.Sales.Infrastructure.Data.Config;

internal sealed class SalesDocumentNumberConfiguration : IEntityTypeConfiguration<SalesDocumentNumber>
{
    public void Configure(EntityTypeBuilder<SalesDocumentNumber> b)
    {
        b.ToTable("SalesDocumentNumbers", table =>
        {
            table.HasCheckConstraint("CK_SalesDocumentNumbers_Year", "[IssueYear] >= 2000 AND [IssueYear] <= 9999");
            table.HasCheckConstraint("CK_SalesDocumentNumbers_Sequence", "[SequenceNumber] > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentType).HasMaxLength(40).IsRequired();
        b.Property(x => x.DisplayNumber).HasMaxLength(16).IsRequired();
        b.Property(x => x.IssuedAt).IsRequired();
        b.HasIndex(x => new { x.SaleId, x.DocumentType }).IsUnique();
        b.HasIndex(x => new { x.DocumentType, x.IssueYear, x.SequenceNumber }).IsUnique();
        b.HasIndex(x => new { x.DocumentType, x.IssueYear, x.DisplayNumber }).IsUnique();
        b.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
    }
}
