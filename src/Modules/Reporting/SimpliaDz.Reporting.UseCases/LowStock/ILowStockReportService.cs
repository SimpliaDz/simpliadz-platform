namespace SimpliaDz.Reporting.UseCases.LowStock;

public sealed record LowStockReportRow(Guid ProductVariantId, Guid ProductId,
    string ProductName, string Sku, Guid LocationId, string LocationName,
    int QuantityOnHand, int ReorderPoint, bool IsOutOfStock);

public sealed record LowStockReportDto(Guid? LocationId, long TotalItems,
    IReadOnlyList<LowStockReportRow> Items);

public interface ILowStockReportService
{
    Task<LowStockReportDto> GetAsync(Guid? locationId, int page, int pageSize,
        CancellationToken ct);
}
