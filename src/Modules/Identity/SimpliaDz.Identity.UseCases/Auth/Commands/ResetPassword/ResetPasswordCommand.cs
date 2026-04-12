using MediatR;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    string Email,
    string ResetToken,
    string NewPassword) : IRequest;
