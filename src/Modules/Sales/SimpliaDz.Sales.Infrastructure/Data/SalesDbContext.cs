using Microsoft.EntityFrameworkCore;

namespace SimpliaDz.Sales.Infrastructure.Data;

public sealed class SalesDbContext : DbContext
{
    public SalesDbContext(DbContextOptions<SalesDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sales");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
    }
}
