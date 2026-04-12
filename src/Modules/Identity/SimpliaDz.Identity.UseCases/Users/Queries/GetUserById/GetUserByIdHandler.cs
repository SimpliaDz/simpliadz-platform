using MediatR;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Identity.Contracts.Users;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.UseCases.Users.Queries.GetUserById;

public sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, UserResponse>
{
    private readonly IIdentityUserService _userService;

    public GetUserByIdHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async Task<UserResponse> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(request.UserId, ct);
        if (user is null)
            throw new NotFoundException("User", request.UserId);

        return user;
    }
}
