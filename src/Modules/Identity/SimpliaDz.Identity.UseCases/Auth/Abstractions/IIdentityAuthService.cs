using SimpliaDz.Identity.Contracts.Auth;

namespace SimpliaDz.Identity.UseCases.Auth.Abstractions;

public interface IIdentityAuthService
{
    Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken ct);
}