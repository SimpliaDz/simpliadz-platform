using Mediator;

namespace Simplia.Identity.UseCases.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;
