namespace Simplia.Inventory.Contracts.Locations;

public sealed record UpdateLocationRequest(
    string Name,
    string? Code,
    string Type,
    string? Address,
    string? Phone);
