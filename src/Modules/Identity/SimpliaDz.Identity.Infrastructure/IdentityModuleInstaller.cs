using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Identity.Infrastructure.Auth;
using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Identity.Infrastructure.Data.Entities;
using SimpliaDz.Identity.Infrastructure.Data.Seeders;
using SimpliaDz.Identity.Infrastructure.Permissions;
using SimpliaDz.Identity.Infrastructure.Roles;
using SimpliaDz.Identity.Infrastructure.Users;
using SimpliaDz.Identity.UseCases.Auth.Abstractions;
using SimpliaDz.Identity.UseCases.Permissions.Abstractions;
using SimpliaDz.Identity.UseCases.Roles.Abstractions;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.Infrastructure;

public sealed class IdentityModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Identity";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));

        services.AddDataProtection();
        
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();
            

        services.AddScoped<IIdentityAuthService, IdentityAuthService>();
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
