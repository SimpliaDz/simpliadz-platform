using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Organization.Core.Entities;

namespace Simplia.Organization.Infrastructure.Data.Config;

internal sealed class WorkSessionCorrectionConfiguration : IEntityTypeConfiguration<WorkSessionCorrection>
{
    public void Configure(EntityTypeBuilder<WorkSessionCorrection> b)
    {
        b.ToTable("WorkSessionCorrections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.HasOne<WorkSession>().WithMany().HasForeignKey(x => x.WorkSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.WorkSessionId, x.CorrectedAt });
    }
}
