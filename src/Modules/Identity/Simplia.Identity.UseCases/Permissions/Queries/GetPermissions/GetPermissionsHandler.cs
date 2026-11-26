using Mediator;
using Simplia.Identity.Contracts.Permissions;
using Simplia.Identity.UseCases.Permissions.Abstractions;

namespace Simplia.Identity.UseCases.Permissions.Queries.GetPermissions;

public sealed class GetPermissionsHandler : IRequestHandler<GetPermissionsQuery, IReadOnlyList<PermissionResponse>>
{
    private readonly IPermissionService _permissionService;

    public GetPermissionsHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async ValueTask<IReadOnlyList<PermissionResponse>> Handle(GetPermissionsQuery request, CancellationToken ct)
    {
        return await _permissionService.GetAllAsync(ct);
    }
}
