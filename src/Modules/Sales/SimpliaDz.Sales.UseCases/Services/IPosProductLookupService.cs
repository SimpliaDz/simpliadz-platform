namespace SimpliaDz.Sales.UseCases.Services;

public interface IPosProductLookupService
{
    Task<PosVariantDto?> FindByBarcodeAsync(string barcode, Guid priceListId,
        Guid? locationId, CancellationToken ct);
    Task<PosVariantDto?> FindBySkuAsync(string sku, Guid priceListId,
        Guid? locationId, CancellationToken ct);
    Task<IReadOnlyList<PosVariantDto>> SearchByProductNameAsync(string namePrefix,
        Guid priceListId, Guid? locationId, int page, int pageSize, CancellationToken ct);
}

public sealed record PosVariantDto(Guid ProductId, string ProductName, Guid VariantId,
    string Sku, string? Barcode, string? Color, string? Size, decimal Price,
    string CurrencyCode, int? QuantityOnHand);
