using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Identity.UseCases.Roles.Abstractions;

namespace SimpliaDz.Identity.UseCases.Roles.Commands.AssignPermissionsToRole;

public sealed class AssignPermissionsToRoleHandler : IRequestHandler<AssignPermissionsToRoleCommand>
{
    private readonly IIdentityRoleService _roleService;

    public AssignPermissionsToRoleHandler(IIdentityRoleService roleService)
    {
        _roleService = roleService;
    }

    public async ValueTask<Unit> Handle(AssignPermissionsToRoleCommand request, CancellationToken ct)
    {
        var role = await _roleService.GetRoleByIdAsync(request.RoleId, ct);
        if (role is null)
            throw new NotFoundException("Role", request.RoleId);

        await _roleService.AssignPermissionsAsync(request.RoleId, request.Permissions, ct);
        return default;
    }
}