namespace SimpliaDz.Identity.Contracts.Users;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool EmailConfirmed,
    string[] Roles,
    string LanguageCode);
