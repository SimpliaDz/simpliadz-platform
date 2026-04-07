namespace SimpliaDz.Identity.Contracts.Auth;

public sealed record LoginResponse(
    string AccessToken,
    int ExpiresMinutes,
    string Email,
    string[] Roles);