namespace Simplia.Reporting.UseCases.Dashboard;

public sealed record DashboardSummaryDto(DateOnly Date, string TimeZone,
    decimal Revenue, decimal GrossProfit, long TransactionCount,
    long ItemsSold, decimal AverageBasket, decimal Expenses,
    long LowStockCount, bool SalesAvailable, bool AccountingAvailable,
    bool InventoryAvailable);

public interface IDashboardSummaryService
{
    Task<DashboardSummaryDto> GetTodayAsync(CancellationToken ct);
}
