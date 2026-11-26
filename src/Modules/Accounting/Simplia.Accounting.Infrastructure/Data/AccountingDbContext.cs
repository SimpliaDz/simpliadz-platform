using Microsoft.EntityFrameworkCore;
using Simplia.Accounting.Core;

namespace Simplia.Accounting.Infrastructure.Data;

public sealed class AccountingDbContext : DbContext
{
    public AccountingDbContext(DbContextOptions<AccountingDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounting");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountingDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        PreventHistoryDeletes();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreventHistoryDeletes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PreventHistoryDeletes();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PreventHistoryDeletes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PreventHistoryDeletes()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<ExpenseCategory>().Any(x => x.State == EntityState.Deleted))
            throw new InvalidOperationException("Expense categories must be deactivated, not deleted.");
        if (ChangeTracker.Entries<Expense>().Any(x => x.State == EntityState.Deleted))
            throw new InvalidOperationException("Expenses cannot be deleted.");
    }
}
