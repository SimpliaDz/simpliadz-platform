using Mediator;

namespace SimpliaDz.Identity.UseCases.Users.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(Guid UserId) : IRequest;
