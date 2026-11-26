namespace Simplia.Identity.Contracts.Roles;

public sealed record RoleWithPermissionsResponse(Guid Id, string Name, string[] Permissions);
