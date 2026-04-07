using Microsoft.EntityFrameworkCore;

namespace SimpliaDz.Reporting.Infrastructure.Data;

public sealed class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("reporting");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
    }
}
