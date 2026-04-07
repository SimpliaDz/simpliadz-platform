using Microsoft.EntityFrameworkCore;

namespace SimpliaDz.Audit.Infrastructure.Data;

public sealed class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }
}
