using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using SimpliaDz.Identity.Contracts.Roles;
using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Identity.Infrastructure.Data.Entities;
using SimpliaDz.Identity.UseCases.Roles.Abstractions;

namespace SimpliaDz.Identity.Infrastructure.Roles;

internal sealed class IdentityRoleService : IIdentityRoleService
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IdentityDbContext _db;

    public IdentityRoleService(RoleManager<AppRole> roleManager, IdentityDbContext db)
    {
        _roleManager = roleManager;
        _db = db;
    }

    public async Task<Guid> CreateRoleAsync(string name, CancellationToken ct)
    {
        if (await _roleManager.RoleExistsAsync(name))
            throw new InvalidOperationException($"Role '{name}' already exists.");

        var role = new AppRole { Name = name };
        var result = await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create role: {errors}");
        }

        return role.Id;
    }

    public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken ct)
    {
        return await _roleManager.Roles
            .Select(r => new RoleResponse(r.Id, r.Name!))
            .ToListAsync(ct);
    }

    public async Task<RoleWithPermissionsResponse?> GetRoleByIdAsync(Guid roleId, CancellationToken ct)
    {
        var role = await _roleManager.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);

        if (role is null) return null;

        var permissions = role.RolePermissions
            .Select(rp => rp.Permission.Name)
            .ToArray();

        return new RoleWithPermissionsResponse(role.Id, role.Name!, permissions);
    }

    public async Task AssignPermissionsAsync(Guid roleId, IList<string> permissions, CancellationToken ct)
    {
        var role = await _roleManager.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw new InvalidOperationException("Role not found.");

        var permissionEntities = await _db.Permissions
            .Where(p => permissions.Contains(p.Name))
            .ToListAsync(ct);

        var existingPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        foreach (var permission in permissionEntities)
        {
            if (!existingPermissionIds.Contains(permission.Id))
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permission.Id
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemovePermissionsAsync(Guid roleId, IList<string> permissions, CancellationToken ct)
    {
        var toRemove = await _db.RolePermissions
            .Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == roleId && permissions.Contains(rp.Permission.Name))
            .ToListAsync(ct);

        _db.RolePermissions.RemoveRange(toRemove);
        await _db.SaveChangesAsync(ct);
    }
}
