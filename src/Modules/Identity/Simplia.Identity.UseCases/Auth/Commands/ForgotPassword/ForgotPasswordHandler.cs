using Mediator;
using Simplia.Identity.Contracts.Auth;
using Simplia.Identity.UseCases.Auth.Abstractions;

namespace Simplia.Identity.UseCases.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResponse>
{
    private readonly IIdentityAuthService _authService;

    public ForgotPasswordHandler(IIdentityAuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<ForgotPasswordResponse> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        // Always returns 200 to avoid user enumeration; token is for dev convenience.
        return await _authService.GeneratePasswordResetTokenAsync(request.Email, ct);
    }
}
