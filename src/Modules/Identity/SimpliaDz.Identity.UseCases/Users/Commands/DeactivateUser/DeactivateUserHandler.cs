using MediatR;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.UseCases.Users.Commands.DeactivateUser;

public sealed class DeactivateUserHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly IIdentityUserService _userService;

    public DeactivateUserHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async Task Handle(DeactivateUserCommand request, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(request.UserId, ct);
        if (user is null)
            throw new NotFoundException(nameof(user), request.UserId);

        await _userService.DeactivateAsync(request.UserId, ct);
    }
}
