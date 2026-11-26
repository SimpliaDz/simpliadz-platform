using Simplia.Identity.Contracts.Users;

namespace Simplia.Identity.UseCases.Users.Abstractions;

public interface IIdentityUserService
{
    Task<Guid> CreateUserAsync(string email, string firstName, string lastName, string password, IList<string>? roles, CancellationToken ct);
    Task<UserResponse?> GetUserByIdAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken ct);
    Task<UserResponse> UpdateLanguageAsync(Guid userId, string languageCode, CancellationToken ct);
    Task AssignRolesAsync(Guid userId, IList<string> roles, CancellationToken ct);
    Task RemoveRolesAsync(Guid userId, IList<string> roles, CancellationToken ct);
    Task ActivateAsync(Guid userId, CancellationToken ct);
    Task DeactivateAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct);
}
