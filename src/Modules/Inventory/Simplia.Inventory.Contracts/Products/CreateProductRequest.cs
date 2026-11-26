namespace Simplia.Inventory.Contracts.Products;

public sealed record CreateProductRequest(
    string Name,
    string? Code,
    string? Description,
    Guid? BrandId,
    Guid? CategoryId);
