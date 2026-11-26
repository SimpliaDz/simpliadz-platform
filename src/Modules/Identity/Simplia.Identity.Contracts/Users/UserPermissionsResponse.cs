namespace Simplia.Identity.Contracts.Users;

public sealed record UserPermissionsResponse(Guid UserId, string[] Permissions);
