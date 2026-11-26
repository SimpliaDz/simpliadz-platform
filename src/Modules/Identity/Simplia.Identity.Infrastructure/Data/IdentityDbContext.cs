using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Simplia.Identity.Core.Entities;

namespace Simplia.Identity.Infrastructure.Data;

public sealed class IdentityDbContext
    : IdentityDbContext<AppUser, AppRole, Guid>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("identity");

        base.OnModelCreating(builder);

        builder.Entity<AppUser>()
            .Property(user => user.LanguageCode)
            .HasMaxLength(2)
            .IsUnicode(false)
            .HasDefaultValue("en");

        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        ProtectLoginHistory();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectLoginHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProtectLoginHistory();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ProtectLoginHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectLoginHistory()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<LoginAttempt>().Any(x =>
            x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Login history is append-only.");
    }
}
