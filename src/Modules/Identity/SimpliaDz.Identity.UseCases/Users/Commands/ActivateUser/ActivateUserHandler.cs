using MediatR;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.UseCases.Users.Commands.ActivateUser;

public sealed class ActivateUserHandler : IRequestHandler<ActivateUserCommand>
{
    private readonly IIdentityUserService _userService;

    public ActivateUserHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async Task Handle(ActivateUserCommand request, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(request.UserId, ct);
        if (user is null)
            throw new NotFoundException(nameof(user), request.UserId);

        await _userService.ActivateAsync(request.UserId, ct);
    }
}
