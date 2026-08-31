using Mediator;

using SimpliaDz.Identity.Contracts.Auth;
using SimpliaDz.Identity.UseCases.Auth.Abstractions;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.RefreshToken;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    private readonly IIdentityAuthService _authService;

    public RefreshTokenHandler(IIdentityAuthService authService) => _authService = authService;

    public async ValueTask<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken ct) =>
        await _authService.RefreshAsync(request.RefreshToken, ct);
}
