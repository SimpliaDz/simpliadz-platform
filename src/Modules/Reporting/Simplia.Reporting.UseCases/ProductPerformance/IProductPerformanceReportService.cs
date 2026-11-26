namespace Simplia.Reporting.UseCases.ProductPerformance;

public enum ProductPerformanceRankBy
{
    Quantity = 1,
    Revenue = 2
}

public sealed record ProductPerformanceDto(Guid ProductId, string ProductName,
    long GrossQuantitySold, long ReturnedQuantity, long NetQuantitySold,
    decimal GrossRevenue, decimal RefundedRevenue, decimal NetRevenue);

public sealed record VariantPerformanceDto(Guid ProductVariantId, Guid ProductId,
    string Sku, string? Barcode, long GrossQuantitySold, long ReturnedQuantity,
    long NetQuantitySold, decimal GrossRevenue, decimal RefundedRevenue,
    decimal NetRevenue);

public interface IProductPerformanceReportService
{
    Task<IReadOnlyList<ProductPerformanceDto>> RankProductsAsync(
        DateOnly fromDate, DateOnly toDate, ProductPerformanceRankBy rankBy,
        int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<VariantPerformanceDto>> GetVariantsAsync(Guid productId,
        DateOnly fromDate, DateOnly toDate, int page, int pageSize,
        CancellationToken ct);
}
