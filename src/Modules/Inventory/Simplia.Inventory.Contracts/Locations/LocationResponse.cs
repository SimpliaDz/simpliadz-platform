namespace Simplia.Inventory.Contracts.Locations;

public sealed record LocationResponse(
    Guid Id,
    string Name,
    string? Code,
    string Type,
    string? Address,
    string? Phone,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
