using Microsoft.EntityFrameworkCore;

namespace SimpliaDz.Purchasing.Infrastructure.Data;

public sealed class PurchasingDbContext : DbContext
{
    public PurchasingDbContext(DbContextOptions<PurchasingDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("purchasing");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PurchasingDbContext).Assembly);
    }
}
