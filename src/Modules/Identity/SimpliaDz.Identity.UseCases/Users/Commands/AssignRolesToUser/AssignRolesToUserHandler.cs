using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.UseCases.Users.Commands.AssignRolesToUser;

public sealed class AssignRolesToUserHandler : IRequestHandler<AssignRolesToUserCommand>
{
    private readonly IIdentityUserService _userService;

    public AssignRolesToUserHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async ValueTask<Unit> Handle(AssignRolesToUserCommand request, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(request.UserId, ct);
        if (user is null)
            throw new NotFoundException(nameof(user), request.UserId);

        await _userService.AssignRolesAsync(request.UserId, request.Roles, ct);
        return default;
    }
}