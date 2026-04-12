namespace SimpliaDz.Identity.UseCases.Auth.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string email, string? userName, IList<string> roles, IList<string> permissions);
    int ExpiresMinutes { get; }
}
