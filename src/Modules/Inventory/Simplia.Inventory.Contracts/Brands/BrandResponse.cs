namespace Simplia.Inventory.Contracts.Brands;

public sealed record BrandResponse(Guid Id, string Name, bool IsActive, DateTimeOffset UpdatedAt);
