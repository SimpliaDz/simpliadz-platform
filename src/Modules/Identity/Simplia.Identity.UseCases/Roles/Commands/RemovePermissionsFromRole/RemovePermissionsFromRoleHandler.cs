using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Identity.UseCases.Roles.Abstractions;

namespace Simplia.Identity.UseCases.Roles.Commands.RemovePermissionsFromRole;

public sealed class RemovePermissionsFromRoleHandler : IRequestHandler<RemovePermissionsFromRoleCommand>
{
    private readonly IIdentityRoleService _roleService;

    public RemovePermissionsFromRoleHandler(IIdentityRoleService roleService)
    {
        _roleService = roleService;
    }

    public async ValueTask<Unit> Handle(RemovePermissionsFromRoleCommand request, CancellationToken ct)
    {
        var role = await _roleService.GetRoleByIdAsync(request.RoleId, ct);
        if (role is null)
            throw new NotFoundException("Role", request.RoleId);

        await _roleService.RemovePermissionsAsync(request.RoleId, request.Permissions, ct);
        return default;
    }
}