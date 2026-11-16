namespace SimpliaDz.Organization.Contracts;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string? LegalName,
    string? Phone,
    string? Email,
    string? Address,
    string CurrencyCode,
    string? DefaultLanguage,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string TimeZoneId);
