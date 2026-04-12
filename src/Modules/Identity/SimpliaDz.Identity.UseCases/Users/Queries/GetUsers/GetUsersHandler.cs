using MediatR;
using SimpliaDz.Identity.Contracts.Users;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.UseCases.Users.Queries.GetUsers;

public sealed class GetUsersHandler : IRequestHandler<GetUsersQuery, IReadOnlyList<UserResponse>>
{
    private readonly IIdentityUserService _userService;

    public GetUsersHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async Task<IReadOnlyList<UserResponse>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        return await _userService.GetUsersAsync(ct);
    }
}
