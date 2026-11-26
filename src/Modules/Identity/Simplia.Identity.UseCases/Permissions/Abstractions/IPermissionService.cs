using Simplia.Identity.Contracts.Permissions;

namespace Simplia.Identity.UseCases.Permissions.Abstractions;

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionResponse>> GetAllAsync(CancellationToken ct);
}
