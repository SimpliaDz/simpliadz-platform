using Mediator;

using SimpliaDz.Identity.UseCases.Auth.Abstractions;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.Logout;

public sealed class LogoutHandler : IRequestHandler<LogoutCommand>
{
    private readonly IIdentityAuthService _authService;

    public LogoutHandler(IIdentityAuthService authService) => _authService = authService;

    public async ValueTask<Unit> Handle(LogoutCommand request, CancellationToken ct)
    {
        await _authService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return default;
    }
}
