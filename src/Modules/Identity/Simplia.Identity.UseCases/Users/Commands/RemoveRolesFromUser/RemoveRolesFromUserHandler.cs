using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Identity.UseCases.Users.Abstractions;

namespace Simplia.Identity.UseCases.Users.Commands.RemoveRolesFromUser;

public sealed class RemoveRolesFromUserHandler : IRequestHandler<RemoveRolesFromUserCommand>
{
    private readonly IIdentityUserService _userService;

    public RemoveRolesFromUserHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async ValueTask<Unit> Handle(RemoveRolesFromUserCommand request, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(request.UserId, ct);
        if (user is null)
            throw new NotFoundException(nameof(user), request.UserId);

        await _userService.RemoveRolesAsync(request.UserId, request.Roles, ct);
        return default;
    }
}