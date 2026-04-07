using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

using SimpliaDz.Identity.UseCases.Auth.Abstractions;

namespace SimpliaDz.Identity.Infrastructure.Auth;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _signingKey;

    public int ExpiresMinutes { get; }

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"] ?? "SimpliaDz";
        _audience = configuration["Jwt:Audience"] ?? "SimpliaDz";
        _signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");

        if (_signingKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey is too short (min 32 chars).");

        ExpiresMinutes = int.TryParse(configuration["Jwt:ExpiresMinutes"], out var m) ? m : 60;
    }

    public string GenerateToken(Guid userId, string email, string? userName, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, userName ?? email)
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(ExpiresMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}