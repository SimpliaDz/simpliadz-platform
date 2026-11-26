using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Identity.Contracts.Users;
using Simplia.Identity.UseCases.Users.Abstractions;

namespace Simplia.Identity.UseCases.Users.Queries.GetUserById;

public sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, UserResponse>
{
    private readonly IIdentityUserService _userService;

    public GetUserByIdHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async ValueTask<UserResponse> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(request.UserId, ct);
        if (user is null)
            throw new NotFoundException("User", request.UserId);

        return user;
    }
}
