namespace SimpliaDz.Inventory.Contracts.Products;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    Guid? BrandId,
    Guid? CategoryId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Guid> AttributeIds,
    IReadOnlyList<ProductVariantResponse> Variants);

public sealed record ProductVariantResponse(
    Guid Id,
    string Sku,
    string? Barcode,
    string? Color,
    string? Size,
    decimal RetailPrice,
    decimal? WholesalePrice,
    bool IsActive);
