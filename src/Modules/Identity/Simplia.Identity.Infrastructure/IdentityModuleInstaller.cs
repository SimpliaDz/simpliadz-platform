using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Identity.Infrastructure.Auth;
using Simplia.Identity.Infrastructure.Data;
using Simplia.Identity.Core.Entities;
using Simplia.Identity.Infrastructure.Data.Seeders;
using Simplia.Identity.Infrastructure.Permissions;
using Simplia.Identity.Infrastructure.Roles;
using Simplia.Identity.Infrastructure.Users;
using Simplia.Identity.UseCases.Auth.Abstractions;
using Simplia.Identity.UseCases.Permissions.Abstractions;
using Simplia.Identity.UseCases.Roles.Abstractions;
using Simplia.Identity.UseCases.Users.Abstractions;

namespace Simplia.Identity.Infrastructure;

public sealed class IdentityModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Identity";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));

        services.AddDataProtection();
        
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();
            

        services.AddScoped<IdentityAuthService>();
        services.AddScoped<IIdentityAuthService>(sp => sp.GetRequiredService<IdentityAuthService>());
        services.AddScoped<IIdentityAccessTokenValidator>(sp => sp.GetRequiredService<IdentityAuthService>());
        services.AddScoped<ILoginAttemptReader, LoginAttemptReader>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IIdentityUserService, IdentityUserService>();
        services.AddScoped<IIdentityRoleService, IdentityRoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
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
        await IdentitySeeder.SeedAsync(scope.ServiceProvider, ct);
    }
}
