namespace Simplia.Identity.Contracts.Roles;

public sealed record RemovePermissionsFromRoleRequest(IList<string> Permissions);
