using Mediator;
using SimpliaDz.Identity.Contracts.Auth;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<ForgotPasswordResponse>;
