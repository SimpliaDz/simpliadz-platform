using MediatR;
using SimpliaDz.Identity.Contracts.Auth;
using SimpliaDz.Identity.UseCases.Auth.Abstractions;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.Login;

public sealed class LoginHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IIdentityAuthService _authService;

    public LoginHandler(IIdentityAuthService authService)
    {
        _authService = authService;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password, ct);

        if (result is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        return result;
    }
}