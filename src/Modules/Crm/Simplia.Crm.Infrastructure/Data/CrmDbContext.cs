using Microsoft.EntityFrameworkCore;
using Simplia.Crm.Core.Entities;

namespace Simplia.Crm.Infrastructure.Data;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<LoyaltyProgram> Programs => Set<LoyaltyProgram>();
    public DbSet<LoyaltyCard> Cards => Set<LoyaltyCard>();
    public DbSet<LoyaltyReward> Rewards => Set<LoyaltyReward>();
    public DbSet<LoyaltyTransaction> Transactions => Set<LoyaltyTransaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("crm");
        builder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        ProtectLedger();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectLedger();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProtectLedger();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ProtectLedger();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectLedger()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<LoyaltyTransaction>().Any(x =>
            x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Loyalty transactions are append-only.");
        if (ChangeTracker.Entries<LoyaltyCard>().Any(x => x.State == EntityState.Deleted) ||
            ChangeTracker.Entries<LoyaltyReward>().Any(x => x.State == EntityState.Deleted) ||
            ChangeTracker.Entries<LoyaltyProgram>().Any(x => x.State == EntityState.Deleted))
            throw new InvalidOperationException("CRM records must be deactivated, not deleted.");
    }
}
