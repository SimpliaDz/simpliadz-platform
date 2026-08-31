using Mediator;

using SimpliaDz.Identity.Contracts.Auth;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResponse>;
