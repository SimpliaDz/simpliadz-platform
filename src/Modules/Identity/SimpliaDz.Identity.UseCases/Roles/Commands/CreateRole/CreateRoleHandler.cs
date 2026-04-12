using MediatR;
using SimpliaDz.Identity.UseCases.Roles.Abstractions;

namespace SimpliaDz.Identity.UseCases.Roles.Commands.CreateRole;

public sealed class CreateRoleHandler : IRequestHandler<CreateRoleCommand, Guid>
{
    private readonly IIdentityRoleService _roleService;

    public CreateRoleHandler(IIdentityRoleService roleService)
    {
        _roleService = roleService;
    }

    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        return await _roleService.CreateRoleAsync(request.Name, ct);
    }
}
