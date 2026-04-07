using Microsoft.EntityFrameworkCore;

namespace SimpliaDz.Accounting.Infrastructure.Data;

public sealed class AccountingDbContext : DbContext
{
    public AccountingDbContext(DbContextOptions<AccountingDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounting");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountingDbContext).Assembly);
    }
}
