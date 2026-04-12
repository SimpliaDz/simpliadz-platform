namespace SimpliaDz.Identity.Contracts.Users;

public sealed record CreateUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    IList<string>? Roles = null);
