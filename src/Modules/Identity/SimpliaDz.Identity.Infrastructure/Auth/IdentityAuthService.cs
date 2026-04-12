using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using SimpliaDz.Identity.Contracts.Auth;
using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Identity.Core.Entities;
using SimpliaDz.Identity.UseCases.Auth.Abstractions;

namespace SimpliaDz.Identity.Infrastructure.Auth;

public sealed class IdentityAuthService : IIdentityAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IdentityDbContext _db;

    public IdentityAuthService(
        UserManager<AppUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IdentityDbContext db)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _db = db;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
            return null;

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isValid)
            return null;

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetEffectivePermissionsAsync(user.Id, ct);

        var token = _jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName,
            roles,
            permissions);

        return new LoginResponse(
            token,
            _jwtTokenGenerator.ExpiresMinutes,
            user.Email ?? string.Empty,
            roles.ToArray());
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Password change failed: {errors}");
        }
    }

    public async Task<ForgotPasswordResponse> GeneratePasswordResetTokenAsync(string email, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(email);

        // Return a dummy token to avoid user enumeration.
        if (user is null)
            return new ForgotPasswordResponse(string.Empty);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return new ForgotPasswordResponse(token);
    }

    public async Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            throw new InvalidOperationException("Invalid reset request.");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Password reset failed: {errors}");
        }
    }

    private async Task<IList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct)
    {
        var roleNames = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId.ToString()) ?? new AppUser());

        return await _db.RolePermissions
            .Where(rp => roleNames.Contains(rp.Role.Name!))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(ct);
    }
}
