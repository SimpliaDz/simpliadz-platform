using SimpliaDz.Identity.Contracts.Permissions;

namespace SimpliaDz.Identity.UseCases.Permissions.Abstractions;

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionResponse>> GetAllAsync(CancellationToken ct);
}
