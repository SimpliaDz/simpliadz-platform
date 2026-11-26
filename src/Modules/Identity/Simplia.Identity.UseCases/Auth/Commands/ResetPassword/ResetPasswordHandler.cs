using Mediator;
using Simplia.Identity.UseCases.Auth.Abstractions;

namespace Simplia.Identity.UseCases.Auth.Commands.ResetPassword;

public sealed class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IIdentityAuthService _authService;

    public ResetPasswordHandler(IIdentityAuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<Unit> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        await _authService.ResetPasswordAsync(request.Email, request.ResetToken, request.NewPassword, ct);
        return default;
    }
}