using Mediator;

namespace Simplia.Identity.UseCases.Users.Commands.AssignRolesToUser;

public sealed record AssignRolesToUserCommand(Guid UserId, IList<string> Roles) : IRequest;
