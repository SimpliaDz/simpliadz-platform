using Mediator;
using Simplia.Identity.Contracts.Auth;

namespace Simplia.Identity.UseCases.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<ForgotPasswordResponse>;
