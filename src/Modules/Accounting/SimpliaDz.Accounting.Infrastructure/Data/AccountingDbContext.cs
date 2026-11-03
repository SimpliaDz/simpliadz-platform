using Microsoft.EntityFrameworkCore;
using SimpliaDz.Accounting.Core;

namespace SimpliaDz.Accounting.Infrastructure.Data;

public sealed class AccountingDbContext : DbContext
{
    public AccountingDbContext(DbContextOptions<AccountingDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounting");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountingDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        PreventCategoryDeletes();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreventCategoryDeletes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PreventCategoryDeletes();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PreventCategoryDeletes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PreventCategoryDeletes()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<ExpenseCategory>().Any(x => x.State == EntityState.Deleted))
            throw new InvalidOperationException("Expense categories must be deactivated, not deleted.");
    }
}
