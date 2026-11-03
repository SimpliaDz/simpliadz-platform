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
    public DbSet<Payment> Payments => Set<Payment>();

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
        if (ChangeTracker.Entries<Payment>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Payment history is immutable.");

        var newPayments = ChangeTracker.Entries<Payment>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity).ToList();
        if (newPayments.Any(payment => !ChangeTracker.Entries<Sale>().Any(entry =>
                entry.Entity.Id == payment.SaleId &&
                entry.State is EntityState.Added or EntityState.Modified)))
            throw new InvalidOperationException("New payments require an updated sale paid amount.");
        foreach (var entry in ChangeTracker.Entries<Sale>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var before = entry.State == EntityState.Added
                ? 0m
                : entry.OriginalValues.GetValue<decimal>(nameof(Sale.PaidAmount));
            var delta = entry.Entity.PaidAmount - before;
            var paymentTotal = newPayments.Where(x => x.SaleId == entry.Entity.Id)
                .Sum(x => x.Amount);
            if (delta != paymentTotal)
                throw new InvalidOperationException("Sale paid amount requires matching new payments.");
        }
    }
}
