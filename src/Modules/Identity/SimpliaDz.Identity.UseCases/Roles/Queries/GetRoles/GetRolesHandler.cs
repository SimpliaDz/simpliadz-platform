using Mediator;
using SimpliaDz.Identity.Contracts.Roles;
using SimpliaDz.Identity.UseCases.Roles.Abstractions;

namespace SimpliaDz.Identity.UseCases.Roles.Queries.GetRoles;

public sealed class GetRolesHandler : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleResponse>>
{
    private readonly IIdentityRoleService _roleService;

    public GetRolesHandler(IIdentityRoleService roleService)
    {
        _roleService = roleService;
    }

    public async ValueTask<IReadOnlyList<RoleResponse>> Handle(GetRolesQuery request, CancellationToken ct)
    {
        return await _roleService.GetRolesAsync(ct);
    }
}
