using Mediator;

namespace SimpliaDz.Identity.UseCases.Users.Commands.AssignRolesToUser;

public sealed record AssignRolesToUserCommand(Guid UserId, IList<string> Roles) : IRequest;
