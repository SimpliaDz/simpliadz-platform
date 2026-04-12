using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.Identity.Infrastructure.Data.Entities;

namespace SimpliaDz.Identity.Infrastructure.Data.Seeders;

internal static class IdentitySeeder
{
    private static readonly (string Name, string DisplayName, string Category)[] PermissionCatalog =
    [
        ("users.view",          "View Users",           "Users"),
        ("users.create",        "Create Users",         "Users"),
        ("users.edit",          "Edit Users",           "Users"),
        ("users.delete",        "Delete Users",         "Users"),
        ("roles.view",          "View Roles",           "Roles"),
        ("roles.manage",        "Manage Roles",         "Roles"),
        ("permissions.view",    "View Permissions",     "Permissions"),
        ("permissions.manage",  "Manage Permissions",   "Permissions"),
        ("products.view",       "View Products",        "Products"),
        ("products.create",     "Create Products",      "Products"),
        ("products.edit",       "Edit Products",        "Products"),
        ("products.delete",     "Delete Products",      "Products"),
    ];

    public static async Task SeedAsync(
        IServiceProvider sp,
        CancellationToken ct)
    {
        var db = sp.GetRequiredService<IdentityDbContext>();
        var roleMgr = sp.GetRequiredService<RoleManager<AppRole>>();
        var userMgr = sp.GetRequiredService<UserManager<AppUser>>();

        await SeedPermissionsAsync(db, ct);
        await SeedAdminRoleAsync(roleMgr, db, ct);
        await SeedAdminUserAsync(userMgr, ct);
    }

    private static async Task SeedPermissionsAsync(IdentityDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Name).ToListAsync(ct);
        var missing = PermissionCatalog.Where(p => !existing.Contains(p.Name)).ToList();

        if (missing.Count == 0) return;

        db.Permissions.AddRange(missing.Select(p => new Permission
        {
            Name = p.Name,
            DisplayName = p.DisplayName,
            Category = p.Category
        }));

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAdminRoleAsync(
        RoleManager<AppRole> roleMgr, IdentityDbContext db, CancellationToken ct)
    {
        const string adminRoleName = "Admin";

        if (!await roleMgr.RoleExistsAsync(adminRoleName))
            await roleMgr.CreateAsync(new AppRole { Name = adminRoleName });

        var role = await roleMgr.Roles
            .Include(r => r.RolePermissions)
            .FirstAsync(r => r.Name == adminRoleName, ct);

        var allPermissions = await db.Permissions.ToListAsync(ct);
        var assignedIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        foreach (var permission in allPermissions.Where(p => !assignedIds.Contains(p.Id)))
        {
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAdminUserAsync(UserManager<AppUser> userMgr, CancellationToken ct)
    {
        const string adminEmail = "admin@dev.local";
        const string adminPassword = "Admin123!"; // dev only

        var user = await userMgr.FindByEmailAsync(adminEmail);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "Dev",
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userMgr.CreateAsync(user, adminPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
                throw new InvalidOperationException($"Failed to create admin user: {errors}");
            }
        }

        if (!await userMgr.IsInRoleAsync(user, "Admin"))
            await userMgr.AddToRoleAsync(user, "Admin");
    }
}
