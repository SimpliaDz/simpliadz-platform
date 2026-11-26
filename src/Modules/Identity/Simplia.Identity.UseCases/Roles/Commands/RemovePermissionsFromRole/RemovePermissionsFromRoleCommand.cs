using Mediator;

namespace Simplia.Identity.UseCases.Roles.Commands.RemovePermissionsFromRole;

public sealed record RemovePermissionsFromRoleCommand(Guid RoleId, IList<string> Permissions) : IRequest;
