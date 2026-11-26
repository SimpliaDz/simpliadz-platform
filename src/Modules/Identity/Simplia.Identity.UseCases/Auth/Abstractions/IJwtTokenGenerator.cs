namespace Simplia.Identity.UseCases.Auth.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string email, string? userName, string securityStamp, IList<string> roles, IList<string> permissions);
    int ExpiresMinutes { get; }
}
