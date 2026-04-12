namespace SimpliaDz.Identity.Contracts.Roles;

public sealed record RemovePermissionsFromRoleRequest(IList<string> Permissions);
