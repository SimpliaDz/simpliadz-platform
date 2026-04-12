using MediatR;

namespace SimpliaDz.Identity.UseCases.Users.Commands.RemoveRolesFromUser;

public sealed record RemoveRolesFromUserCommand(Guid UserId, IList<string> Roles) : IRequest;
