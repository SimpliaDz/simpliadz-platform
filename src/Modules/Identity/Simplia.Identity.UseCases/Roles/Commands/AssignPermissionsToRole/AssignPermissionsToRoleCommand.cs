using Mediator;

namespace Simplia.Identity.UseCases.Roles.Commands.AssignPermissionsToRole;

public sealed record AssignPermissionsToRoleCommand(Guid RoleId, IList<string> Permissions) : IRequest;
