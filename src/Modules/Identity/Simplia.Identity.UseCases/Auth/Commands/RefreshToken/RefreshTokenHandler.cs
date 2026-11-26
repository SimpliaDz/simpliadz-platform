using Mediator;

using Simplia.Identity.Contracts.Auth;
using Simplia.Identity.UseCases.Auth.Abstractions;

namespace Simplia.Identity.UseCases.Auth.Commands.RefreshToken;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    private readonly IIdentityAuthService _authService;

    public RefreshTokenHandler(IIdentityAuthService authService) => _authService = authService;

    public async ValueTask<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken ct) =>
        await _authService.RefreshAsync(request.RefreshToken, ct);
}
