using Microsoft.EntityFrameworkCore;
using Simplia.Audit.Core;

namespace Simplia.Audit.Infrastructure.Data;

public sealed class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditEntry> Entries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }

    public override int SaveChanges() { RejectChanges(); return base.SaveChanges(); }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    { RejectChanges(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    { RejectChanges(); return base.SaveChangesAsync(cancellationToken); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    { RejectChanges(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }

    private void RejectChanges()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<AuditEntry>().Any(x =>
                x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit entries are append-only.");
    }
}
