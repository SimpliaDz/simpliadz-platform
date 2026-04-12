using MediatR;
using SimpliaDz.Identity.UseCases.Auth.Abstractions;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.ChangePassword;

public sealed class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IIdentityAuthService _authService;

    public ChangePasswordHandler(IIdentityAuthService authService)
    {
        _authService = authService;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        await _authService.ChangePasswordAsync(request.UserId, request.CurrentPassword, request.NewPassword, ct);
    }
}
