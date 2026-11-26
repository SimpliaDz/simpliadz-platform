using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Identity.Contracts.Users;
using Simplia.Identity.UseCases.Users.Abstractions;

namespace Simplia.Identity.UseCases.Users.Queries.GetUserPermissions;

public sealed class GetUserPermissionsHandler : IRequestHandler<GetUserPermissionsQuery, UserPermissionsResponse>
{
    private readonly IIdentityUserService _userService;

    public GetUserPermissionsHandler(IIdentityUserService userService)
    {
        _userService = userService;
    }

    public async ValueTask<UserPermissionsResponse> Handle(GetUserPermissionsQuery request, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(request.UserId, ct);
        if (user is null)
            throw new NotFoundException("User", request.UserId);

        var permissions = await _userService.GetEffectivePermissionsAsync(request.UserId, ct);
        return new UserPermissionsResponse(request.UserId, permissions.ToArray());
    }
}
