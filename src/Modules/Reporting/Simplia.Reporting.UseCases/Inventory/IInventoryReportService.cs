namespace Simplia.Reporting.UseCases.Inventory;

public sealed record InventoryReportRow(Guid ProductVariantId, Guid ProductId,
    string ProductName, string Sku, string? Barcode, long QuantityOnHand,
    decimal? UnitCost, decimal? StockValue);

public sealed record InventoryReportDto(Guid? LocationId, bool IncludeZeroStock,
    long TotalVariants, long TotalQuantityOnHand, decimal KnownStockValue,
    long UnvaluedQuantity, decimal? TotalStockValue,
    IReadOnlyList<InventoryReportRow> Items);

public interface IInventoryReportService
{
    Task<InventoryReportDto> GetAsync(Guid? locationId, bool includeZeroStock,
        int page, int pageSize, CancellationToken ct);
}
