namespace SimpliaDz.Organization.Contracts;

public sealed record UpdateOrganizationRequest(
    string Name,
    string? LegalName,
    string? Phone,
    string? Email,
    string? Address,
    string? CurrencyCode,
    string? DefaultLanguage,
    bool IsActive = true,
    string? TimeZoneId = null);
