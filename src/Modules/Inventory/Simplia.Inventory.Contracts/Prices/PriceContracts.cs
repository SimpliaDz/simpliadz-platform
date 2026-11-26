namespace Simplia.Inventory.Contracts.Prices;

public sealed record CreatePriceListRequest(string Name, string CurrencyCode, string Type);
public sealed record SetVariantPriceRequest(decimal Price);
public sealed record PriceListResponse(Guid Id, string Name, string CurrencyCode, string Type, bool IsActive);
public sealed record VariantPriceResponse(Guid PriceListId, Guid ProductVariantId, string CurrencyCode, decimal Price);
