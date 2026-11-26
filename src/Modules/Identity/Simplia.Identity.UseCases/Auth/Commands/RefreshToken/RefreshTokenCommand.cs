using Mediator;

using Simplia.Identity.Contracts.Auth;

namespace Simplia.Identity.UseCases.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResponse>;
