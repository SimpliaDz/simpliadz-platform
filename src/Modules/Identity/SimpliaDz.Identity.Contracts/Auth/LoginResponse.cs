namespace SimpliaDz.Identity.Contracts.Auth;

public sealed record LoginResponse(
    string AccessToken,
    int ExpiresMinutes,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string Email,
    string[] Roles);
