using Mediator;

namespace SimpliaDz.Identity.UseCases.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid UserId) : IRequest;
