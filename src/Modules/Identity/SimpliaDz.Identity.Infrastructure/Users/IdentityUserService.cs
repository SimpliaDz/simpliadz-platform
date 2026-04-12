using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using SimpliaDz.Identity.Contracts.Users;
using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Identity.Infrastructure.Data.Entities;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.Infrastructure.Users;

internal sealed class IdentityUserService : IIdentityUserService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IdentityDbContext _db;

    public IdentityUserService(UserManager<AppUser> userManager, IdentityDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<Guid> CreateUserAsync(
        string email, string firstName, string lastName, string password,
        IList<string>? roles, CancellationToken ct)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        if (roles is { Count: > 0 })
        {
            var roleResult = await _userManager.AddToRolesAsync(user, roles);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign roles: {errors}");
            }
        }

        return user.Id;
    }

    public async Task<UserResponse?> GetUserByIdAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return Map(user, roles);
    }

    public async Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken ct)
    {
        var users = await _userManager.Users.ToListAsync(ct);
        var result = new List<UserResponse>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(Map(user, roles));
        }

        return result;
    }

    public async Task AssignRolesAsync(Guid userId, IList<string> roles, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        var result = await _userManager.AddToRolesAsync(user, roles);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to assign roles: {errors}");
        }
    }

    public async Task RemoveRolesAsync(Guid userId, IList<string> roles, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        var result = await _userManager.RemoveFromRolesAsync(user, roles);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to remove roles: {errors}");
        }
    }

    public async Task ActivateAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        user.IsActive = true;
        await _userManager.UpdateAsync(user);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        user.IsActive = false;
        await _userManager.UpdateAsync(user);
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return [];

        var roleNames = await _userManager.GetRolesAsync(user);

        return await _db.RolePermissions
            .Where(rp => roleNames.Contains(rp.Role.Name!))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(ct);
    }

    private static UserResponse Map(AppUser user, IList<string> roles) =>
        new(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, user.IsActive, user.EmailConfirmed, roles.ToArray());
}
