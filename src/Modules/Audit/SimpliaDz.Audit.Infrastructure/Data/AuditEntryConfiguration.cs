using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Audit.Core;

namespace SimpliaDz.Audit.Infrastructure.Data;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("AuditEntries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.OldValues).HasMaxLength(4000);
        b.Property(x => x.NewValues).HasMaxLength(4000);
        b.Property(x => x.CorrelationId).HasMaxLength(100);
        b.Property(x => x.Timestamp).IsRequired();
        b.HasIndex(x => new { x.Timestamp, x.Id });
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.Timestamp });
        b.HasIndex(x => new { x.UserId, x.Timestamp });
    }
}
