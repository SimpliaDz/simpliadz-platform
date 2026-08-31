using Mediator;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;
