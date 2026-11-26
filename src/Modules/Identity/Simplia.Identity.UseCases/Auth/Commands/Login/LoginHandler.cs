using Mediator;
using Simplia.Identity.Contracts.Auth;
using Simplia.Identity.UseCases.Auth.Abstractions;

namespace Simplia.Identity.UseCases.Auth.Commands.Login;

public sealed class LoginHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IIdentityAuthService _authService;

    public LoginHandler(IIdentityAuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<LoginResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password,
            request.IpAddress, request.UserAgent, ct);

        if (result is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        return result;
    }
}
