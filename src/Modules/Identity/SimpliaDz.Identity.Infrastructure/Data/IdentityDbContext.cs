using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Identity.Infrastructure.Data.Entities;

namespace SimpliaDz.Identity.Infrastructure.Data;

public sealed class IdentityDbContext
    : IdentityDbContext<AppUser, AppRole, Guid>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Put ALL ASP.NET Identity tables under schema: identity
        builder.HasDefaultSchema("identity");

        base.OnModelCreating(builder);

        // Later, we can configure table names if you want:
        // builder.Entity<AppUser>().ToTable("Users");
        // builder.Entity<AppRole>().ToTable("Roles");
    }
}
