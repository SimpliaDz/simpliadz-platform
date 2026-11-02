namespace SimpliaDz.Inventory.Contracts.Variants;

public sealed record CreateVariantRequest(
    string Sku, string? Barcode, decimal RetailPrice, decimal? WholesalePrice,
    IReadOnlyList<Guid> AttributeValueIds);

public sealed record VariantValueResponse(Guid AttributeValueId, Guid AttributeId, string AttributeName, string Value, int DisplayOrder);

public sealed record VariantResponse(
    Guid Id, Guid ProductId, string Sku, string? Barcode, bool IsActive,
    decimal RetailPrice, decimal? WholesalePrice, IReadOnlyList<VariantValueResponse> AttributeValues);
