using Mediator;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.UseCases.Users.Commands.CreateUser;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly IIdentityUserService _userService;

    public CreateUserHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async ValueTask<Guid> Handle(CreateUserCommand request, CancellationToken ct)
    {
        return await _userService.CreateUserAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.Roles,
            ct);
    }
}
