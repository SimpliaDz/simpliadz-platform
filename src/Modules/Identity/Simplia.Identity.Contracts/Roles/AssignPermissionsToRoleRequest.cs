namespace Simplia.Identity.Contracts.Roles;

public sealed record AssignPermissionsToRoleRequest(IList<string> Permissions);
