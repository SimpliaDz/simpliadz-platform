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
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnLine> SaleReturnLines => Set<SaleReturnLine>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<CashRegisterAssignment> CashRegisterAssignments => Set<CashRegisterAssignment>();
    public DbSet<SalesDocumentNumber> SalesDocumentNumbers => Set<SalesDocumentNumber>();

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
        var sales = ChangeTracker.Entries<Sale>().ToArray();
        if (sales.Any(entry => entry.State == EntityState.Deleted))
            throw new InvalidOperationException("Sale history cannot be deleted.");
        foreach (var line in ChangeTracker.Entries<SaleLine>().Where(entry => entry.State == EntityState.Deleted))
        {
            var saleId = line.Property(x => x.SaleId).OriginalValue;
            // Saving the parent revision also checks its row version against concurrent checkout.
            if (!sales.Any(entry => entry.Entity.Id == saleId &&
                    entry.State == EntityState.Modified &&
                    entry.Property(x => x.Revision).IsModified &&
                    entry.Property(x => x.Status).OriginalValue == SaleStatus.Draft &&
                    (entry.Entity.Status == SaleStatus.Draft ||
                     (entry.Entity.Status == SaleStatus.Cancelled && entry.Entity.Lines.Count == 0))))
                throw new InvalidOperationException("Sale history cannot be deleted.");
        }
        if (ChangeTracker.Entries<Payment>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Payment history is immutable.");
        if (ChangeTracker.Entries<SaleReturn>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<SaleReturnLine>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Sale return history is immutable.");
        if (ChangeTracker.Entries<SalesDocumentNumber>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Issued sales document numbers are immutable.");
        if (ChangeTracker.Entries<CashMovement>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Cash movement history is immutable.");
        if (ChangeTracker.Entries<CashSession>().Any(entry => entry.State == EntityState.Deleted) ||
            ChangeTracker.Entries<CashRegister>().Any(entry => entry.State == EntityState.Deleted) ||
            ChangeTracker.Entries<CashRegisterAssignment>().Any(entry => entry.State == EntityState.Deleted))
            throw new InvalidOperationException("Cash registers and sessions cannot be deleted.");

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
