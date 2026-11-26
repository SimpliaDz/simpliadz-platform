using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Identity.Contracts.Roles;
using Simplia.Identity.UseCases.Roles.Abstractions;

namespace Simplia.Identity.UseCases.Roles.Queries.GetRoleById;

public sealed class GetRoleByIdHandler : IRequestHandler<GetRoleByIdQuery, RoleWithPermissionsResponse>
{
    private readonly IIdentityRoleService _roleService;

    public GetRoleByIdHandler(IIdentityRoleService roleService)
    {
        _roleService = roleService;
    }

    public async ValueTask<RoleWithPermissionsResponse> Handle(GetRoleByIdQuery request, CancellationToken ct)
    {
        var role = await _roleService.GetRoleByIdAsync(request.RoleId, ct);
        if (role is null)
            throw new NotFoundException("Role", request.RoleId);

        return role;
    }
}
