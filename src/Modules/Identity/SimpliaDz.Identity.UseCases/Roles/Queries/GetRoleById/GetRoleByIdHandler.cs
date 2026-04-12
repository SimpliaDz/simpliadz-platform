using MediatR;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Identity.Contracts.Roles;
using SimpliaDz.Identity.UseCases.Roles.Abstractions;

namespace SimpliaDz.Identity.UseCases.Roles.Queries.GetRoleById;

public sealed class GetRoleByIdHandler : IRequestHandler<GetRoleByIdQuery, RoleWithPermissionsResponse>
{
    private readonly IIdentityRoleService _roleService;

    public GetRoleByIdHandler(IIdentityRoleService roleService)
    {
        _roleService = roleService;
    }

    public async Task<RoleWithPermissionsResponse> Handle(GetRoleByIdQuery request, CancellationToken ct)
    {
        var role = await _roleService.GetRoleByIdAsync(request.RoleId, ct);
        if (role is null)
            throw new NotFoundException("Role", request.RoleId);

        return role;
    }
}
