using MediatR;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Identity.UseCases.Roles.Abstractions;

namespace SimpliaDz.Identity.UseCases.Roles.Commands.RemovePermissionsFromRole;

public sealed class RemovePermissionsFromRoleHandler : IRequestHandler<RemovePermissionsFromRoleCommand>
{
    private readonly IIdentityRoleService _roleService;

    public RemovePermissionsFromRoleHandler(IIdentityRoleService roleService)
    {
        _roleService = roleService;
    }

    public async Task Handle(RemovePermissionsFromRoleCommand request, CancellationToken ct)
    {
        var role = await _roleService.GetRoleByIdAsync(request.RoleId, ct);
        if (role is null)
            throw new NotFoundException("Role", request.RoleId);

        await _roleService.RemovePermissionsAsync(request.RoleId, request.Permissions, ct);
    }
}
