namespace SimpliaDz.Identity.Contracts.Users;

public sealed record AssignRolesToUserRequest(IList<string> Roles);
