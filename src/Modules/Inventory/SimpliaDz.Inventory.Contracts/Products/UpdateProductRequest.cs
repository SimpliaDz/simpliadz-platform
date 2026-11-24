namespace SimpliaDz.Inventory.Contracts.Products;

public sealed record UpdateProductRequest(
    string Name,
    string? Code,
    string? Description,
    Guid? BrandId,
    Guid? CategoryId,
    IReadOnlyList<Guid>? AttributeIds = null);
