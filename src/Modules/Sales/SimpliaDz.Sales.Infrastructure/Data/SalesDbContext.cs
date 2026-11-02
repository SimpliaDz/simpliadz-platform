using Microsoft.EntityFrameworkCore;
using SimpliaDz.Sales.Core.Entities;

namespace SimpliaDz.Sales.Infrastructure.Data;

public sealed class SalesDbContext : DbContext
{
    public SalesDbContext(DbContextOptions<SalesDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sales");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        PreventCustomerDeletes();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreventCustomerDeletes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PreventCustomerDeletes();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PreventCustomerDeletes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PreventCustomerDeletes()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<Customer>().Any(entry => entry.State == EntityState.Deleted))
            throw new InvalidOperationException("Customers must be deactivated, not deleted.");
        if (ChangeTracker.Entries<Sale>().Any(entry => entry.State == EntityState.Deleted) ||
            ChangeTracker.Entries<SaleLine>().Any(entry => entry.State == EntityState.Deleted))
            throw new InvalidOperationException("Sale history cannot be deleted.");
    }
}
