using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Organization.Core.Entities;

namespace Simplia.Organization.Infrastructure.Data.Config;

internal sealed class WorkSessionConfiguration : IEntityTypeConfiguration<WorkSession>
{
    public void Configure(EntityTypeBuilder<WorkSession> b)
    {
        b.ToTable("WorkSessions", table => table.HasCheckConstraint(
            "CK_WorkSessions_ClockOrder", "[ClockOutAt] IS NULL OR [ClockOutAt] > [ClockInAt]"));
        b.HasKey(x => x.Id);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[ClockOutAt] IS NULL");
        b.HasIndex(x => new { x.UserId, x.ClockInAt });
    }
}
