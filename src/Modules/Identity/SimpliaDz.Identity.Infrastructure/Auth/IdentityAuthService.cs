using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using SimpliaDz.Identity.Contracts.Auth;
using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Identity.Core.Entities;
using SimpliaDz.Identity.UseCases.Auth.Abstractions;

namespace SimpliaDz.Identity.Infrastructure.Auth;

public sealed class IdentityAuthService : IIdentityAuthService, IIdentityAccessTokenValidator
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IdentityDbContext _db;
    private readonly int _refreshTokenExpiresDays;
    private readonly bool _exposePasswordResetToken;

    public IdentityAuthService(
        UserManager<AppUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IdentityDbContext db,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _db = db;
        _refreshTokenExpiresDays = int.TryParse(configuration["Jwt:RefreshTokenExpiresDays"], out var days)
            ? days
            : 14;
        _exposePasswordResetToken = bool.TryParse(
            configuration["Identity:ExposePasswordResetToken"], out var exposeToken) && exposeToken;

        if (_refreshTokenExpiresDays <= 0)
            throw new InvalidOperationException("Jwt:RefreshTokenExpiresDays must be greater than zero.");
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password,
        string? ipAddress, string? userAgent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive || await _userManager.IsLockedOutAsync(user))
        {
            await RecordLoginAttemptAsync(user?.Id, email, false,
                ipAddress, userAgent, ct);
            return null;
        }

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isValid)
        {
            await using var failedTransaction = await _db.Database.BeginTransactionAsync(ct);
            var result = await _userManager.AccessFailedAsync(user);
            EnsureSucceeded(result, "record failed login");
            await RecordLoginAttemptAsync(user.Id, email, false,
                ipAddress, userAgent, ct);
            await failedTransaction.CommitAsync(ct);
            return null;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var reset = await _userManager.ResetAccessFailedCountAsync(user);
        EnsureSucceeded(reset, "reset failed login count");
        var response = await CreateSessionAsync(user, ipAddress, userAgent, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _db.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (!storedToken.IsActive(utcNow))
        {
            // A rotated token being used again indicates likely token theft. Revoke
            // every remaining session for that user so the token family cannot grow.
            if (storedToken.RevokedAtUtc is not null && storedToken.ReplacedByTokenHash is not null)
            {
                await RevokeAllRefreshTokensAsync(storedToken.UserId, utcNow, ct);
                var stampResult = await _userManager.UpdateSecurityStampAsync(storedToken.User);
                EnsureSucceeded(stampResult, "invalidate compromised sessions");
                await _db.SaveChangesAsync(ct);
            }

            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (!storedToken.User.IsActive || await _userManager.IsLockedOutAsync(storedToken.User))
        {
            storedToken.RevokedAtUtc = utcNow;
            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("The user account is not active.");
        }

        var replacement = CreateRefreshToken(storedToken.UserId, utcNow);
        storedToken.RevokedAtUtc = utcNow;
        storedToken.ReplacedByTokenHash = replacement.Entity.TokenHash;
        _db.RefreshTokens.Add(replacement.Entity);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        return await CreateLoginResponseAsync(
            storedToken.User,
            replacement.RawToken,
            replacement.Entity.ExpiresAtUtc,
            ct);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _db.RefreshTokens
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

        // Logout is idempotent and does not reveal whether a token exists.
        if (storedToken is null || storedToken.RevokedAtUtc is not null)
            return;

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> IsValidAsync(Guid userId, string securityStamp, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || await _userManager.IsLockedOutAsync(user))
            return false;

        var currentStamp = await _userManager.GetSecurityStampAsync(user);
        return string.Equals(currentStamp, securityStamp, StringComparison.Ordinal);
    }

    private async Task<LoginResponse> CreateSessionAsync(AppUser user,
        string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        var refreshToken = CreateRefreshToken(user.Id, utcNow);
        var response = await CreateLoginResponseAsync(
            user, refreshToken.RawToken, refreshToken.Entity.ExpiresAtUtc, ct);
        _db.RefreshTokens.Add(refreshToken.Entity);
        _db.LoginAttempts.Add(new LoginAttempt(user.Id, user.Email ?? string.Empty,
            true, ipAddress, userAgent));
        await _db.SaveChangesAsync(ct);
        return response;
    }

    private async Task RecordLoginAttemptAsync(Guid? userId, string email,
        bool succeeded, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        _db.LoginAttempts.Add(new LoginAttempt(userId, email, succeeded,
            ipAddress, userAgent));
        await _db.SaveChangesAsync(ct);
    }

    private async Task<LoginResponse> CreateLoginResponseAsync(
        AppUser user,
        string rawRefreshToken,
        DateTime refreshTokenExpiresAtUtc,
        CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetEffectivePermissionsAsync(user.Id, ct);
        var securityStamp = await _userManager.GetSecurityStampAsync(user);
        var accessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtTokenGenerator.ExpiresMinutes);

        var token = _jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName,
            securityStamp,
            roles,
            permissions);

        return new LoginResponse(
            token,
            _jwtTokenGenerator.ExpiresMinutes,
            accessTokenExpiresAtUtc,
            rawRefreshToken,
            refreshTokenExpiresAtUtc,
            user.Email ?? string.Empty,
            roles.ToArray(),
            user.Id);
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

        await _userManager.UpdateSecurityStampAsync(user);
        await RevokeAllRefreshTokensAsync(user.Id, DateTime.UtcNow, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ForgotPasswordResponse> GeneratePasswordResetTokenAsync(string email, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(email);

        // Return a dummy token to avoid user enumeration.
        if (user is null || !user.IsActive)
            return new ForgotPasswordResponse(string.Empty);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return new ForgotPasswordResponse(_exposePasswordResetToken ? token : string.Empty);
    }

    public async Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            throw new InvalidOperationException("Invalid reset request.");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Invalid reset request.");

        await _userManager.UpdateSecurityStampAsync(user);
        await RevokeAllRefreshTokensAsync(user.Id, DateTime.UtcNow, ct);
        await _db.SaveChangesAsync(ct);
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

    private (RefreshToken Entity, string RawToken) CreateRefreshToken(Guid userId, DateTime utcNow)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = HashToken(rawToken),
                CreatedAtUtc = utcNow,
                ExpiresAtUtc = utcNow.AddDays(_refreshTokenExpiresDays)
            },
            rawToken);
    }

    private async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime utcNow, CancellationToken ct)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > utcNow)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
            token.RevokedAtUtc = utcNow;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Failed to {operation}: {errors}");
    }
}
