using Microsoft.EntityFrameworkCore;
using SimpliaDz.Purchasing.Core.Entities;

namespace SimpliaDz.Purchasing.Infrastructure.Data;

public sealed class PurchasingDbContext : DbContext
{
    public PurchasingDbContext(DbContextOptions<PurchasingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("purchasing");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PurchasingDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        EnsureSuppliersAreNotDeleted();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureSuppliersAreNotDeleted();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureSuppliersAreNotDeleted();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureSuppliersAreNotDeleted();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureSuppliersAreNotDeleted()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<Supplier>().Any(entry => entry.State == EntityState.Deleted))
            throw new InvalidOperationException("Suppliers must be deactivated, not deleted.");
    }
}
