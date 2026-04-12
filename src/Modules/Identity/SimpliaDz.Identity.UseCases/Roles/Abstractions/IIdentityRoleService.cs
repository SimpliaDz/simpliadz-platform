using SimpliaDz.Identity.Contracts.Roles;

namespace SimpliaDz.Identity.UseCases.Roles.Abstractions;

public interface IIdentityRoleService
{
    Task<Guid> CreateRoleAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken ct);
    Task<RoleWithPermissionsResponse?> GetRoleByIdAsync(Guid roleId, CancellationToken ct);
    Task AssignPermissionsAsync(Guid roleId, IList<string> permissions, CancellationToken ct);
    Task RemovePermissionsAsync(Guid roleId, IList<string> permissions, CancellationToken ct);
}
