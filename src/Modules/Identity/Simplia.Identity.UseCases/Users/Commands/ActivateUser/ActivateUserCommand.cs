using Mediator;

namespace Simplia.Identity.UseCases.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid UserId) : IRequest;
