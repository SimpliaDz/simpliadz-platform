/*
Purpose

strongly typed JWT config instead of reading raw strings everywhere
*/

namespace SimpliaDz.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "SimpliaDz";
    public string Audience { get; init; } = "SimpliaDz";
    public string SigningKey { get; init; } = string.Empty;
    public int ExpiresMinutes { get; init; } = 60;
}
