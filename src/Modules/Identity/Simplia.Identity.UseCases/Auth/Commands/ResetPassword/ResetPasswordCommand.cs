using Mediator;

namespace Simplia.Identity.UseCases.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    string Email,
    string ResetToken,
    string NewPassword) : IRequest;
