using Mediator;

namespace Simplia.Identity.UseCases.Users.Commands.RemoveRolesFromUser;

public sealed record RemoveRolesFromUserCommand(Guid UserId, IList<string> Roles) : IRequest;
