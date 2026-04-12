namespace SimpliaDz.Identity.Contracts.Users;

public sealed record RemoveRolesFromUserRequest(IList<string> Roles);
