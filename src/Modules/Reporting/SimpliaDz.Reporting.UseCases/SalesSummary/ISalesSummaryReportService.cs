namespace SimpliaDz.Reporting.UseCases.SalesSummary;

public sealed record SalesSummaryDto(DateOnly FromDate, DateOnly ToDate,
    decimal Revenue, decimal CostOfGoodsSold, decimal GrossProfit,
    long TransactionCount, long ItemsSold, decimal AverageTransactionValue);

public interface ISalesSummaryReportService
{
    Task<SalesSummaryDto> GetAsync(DateOnly fromDate, DateOnly toDate, Guid? locationId,
        CancellationToken ct);
}
