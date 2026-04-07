using Microsoft.AspNetCore.Identity;

using SimpliaDz.Identity.Contracts.Auth;
using SimpliaDz.Identity.Infrastructure.Data.Entities;
using SimpliaDz.Identity.UseCases.Auth.Abstractions;

namespace SimpliaDz.Identity.Infrastructure.Auth;

public sealed class IdentityAuthService : IIdentityAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public IdentityAuthService(
        UserManager<AppUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return null;

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isValid)
            return null;

        var roles = await _userManager.GetRolesAsync(user);

        var token = _jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName,
            roles);

        return new LoginResponse(
            token,
            _jwtTokenGenerator.ExpiresMinutes,
            user.Email ?? string.Empty,
            roles.ToArray());
    }
}