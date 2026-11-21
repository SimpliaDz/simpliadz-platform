using Mediator;
using SimpliaDz.Identity.Contracts.Auth;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password,
    string? IpAddress = null, string? UserAgent = null) : IRequest<LoginResponse>;
