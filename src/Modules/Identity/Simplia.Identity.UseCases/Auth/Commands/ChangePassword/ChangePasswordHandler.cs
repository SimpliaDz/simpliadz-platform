using Mediator;
using Simplia.Identity.UseCases.Auth.Abstractions;

namespace Simplia.Identity.UseCases.Auth.Commands.ChangePassword;

public sealed class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IIdentityAuthService _authService;

    public ChangePasswordHandler(IIdentityAuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<Unit> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        await _authService.ChangePasswordAsync(request.UserId, request.CurrentPassword, request.NewPassword, ct);
        return default;
    }
}