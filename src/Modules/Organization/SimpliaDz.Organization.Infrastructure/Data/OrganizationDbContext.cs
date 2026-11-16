using Microsoft.EntityFrameworkCore;
using SimpliaDz.Organization.Core.Entities;
using OrganizationEntity = SimpliaDz.Organization.Core.Entities.Organization;

namespace SimpliaDz.Organization.Infrastructure.Data;

public sealed class OrganizationDbContext : DbContext
{
    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();
    public DbSet<WorkSessionCorrection> WorkSessionCorrections => Set<WorkSessionCorrection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("organization");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        ProtectWorkHistory();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectWorkHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProtectWorkHistory();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ProtectWorkHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectWorkHistory()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<WorkSessionCorrection>().Any(x =>
                x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<WorkSession>().Any(x => x.State == EntityState.Deleted))
            throw new InvalidOperationException("Worked-hours history cannot be deleted or corrected in place.");
    }
}
