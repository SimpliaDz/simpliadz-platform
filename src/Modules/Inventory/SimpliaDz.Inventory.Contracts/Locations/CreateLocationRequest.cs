namespace SimpliaDz.Inventory.Contracts.Locations;

public sealed record CreateLocationRequest(
    string Name,
    string? Code,
    string Type,
    string? Address,
    string? Phone);
