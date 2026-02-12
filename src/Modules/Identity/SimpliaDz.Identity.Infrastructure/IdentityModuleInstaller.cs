using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Identity.Infrastructure.Data.Entities;

namespace SimpliaDz.Identity.Infrastructure;

public sealed class IdentityModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Identity";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));

        // We will register Identity services (UserManager, SignInManager, etc.)
        // later in the API host. The migrator only needs the DbContext.
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<IdentityDbContext>();
    }

    public async Task MigrateAsync(IServiceProvider sp, CancellationToken ct)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(ct);
    }

    public async Task SeedAsync(IServiceProvider sp, CancellationToken ct)
    {
        await using var scope = sp.CreateAsyncScope();
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        const string adminRole = "Admin";
        const string adminEmail = "admin@dev.local";
        const string adminPassword = "Admin123!"; // dev only

        // Role
        if (!await roleMgr.RoleExistsAsync(adminRole))
        {
            await roleMgr.CreateAsync(new AppRole { Name = adminRole });
        }

        // User
        var user = await userMgr.FindByEmailAsync(adminEmail);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userMgr.CreateAsync(user, adminPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
                throw new InvalidOperationException($"Failed to create admin user: {errors}");
            }
        }

        // Assign role
        if (!await userMgr.IsInRoleAsync(user, adminRole))
        {
            await userMgr.AddToRoleAsync(user, adminRole);
        }
    }
}
