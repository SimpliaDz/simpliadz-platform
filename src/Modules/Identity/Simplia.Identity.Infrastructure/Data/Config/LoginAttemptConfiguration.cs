using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Identity.Core.Entities;

namespace Simplia.Identity.Infrastructure.Data.Config;

internal sealed class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("LoginAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.OccurredAt });
        builder.HasIndex(x => new { x.Email, x.OccurredAt });
        builder.HasIndex(x => new { x.OccurredAt, x.Id });
    }
}
