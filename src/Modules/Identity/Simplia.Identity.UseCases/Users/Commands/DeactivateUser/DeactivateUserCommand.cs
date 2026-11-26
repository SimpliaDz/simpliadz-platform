using Mediator;

namespace Simplia.Identity.UseCases.Users.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(Guid UserId) : IRequest;
