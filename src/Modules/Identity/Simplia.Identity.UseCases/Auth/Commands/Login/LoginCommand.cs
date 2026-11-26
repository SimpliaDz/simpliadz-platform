using Mediator;
using Simplia.Identity.Contracts.Auth;

namespace Simplia.Identity.UseCases.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password,
    string? IpAddress = null, string? UserAgent = null) : IRequest<LoginResponse>;
