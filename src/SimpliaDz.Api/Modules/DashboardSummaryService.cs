using SimpliaDz.Accounting.UseCases.Services;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Reporting.UseCases.Dashboard;
using SimpliaDz.Reporting.UseCases.LowStock;
using SimpliaDz.Reporting.UseCases.SalesSummary;

namespace SimpliaDz.Api.Modules;

public sealed class DashboardSummaryService(ITenantContext tenant,
    ISalesSummaryReportService sales, IExpenseTotalReader expenses,
    ILowStockReportService lowStock, TimeProvider timeProvider)
    : IDashboardSummaryService
{
    public async Task<DashboardSummaryDto> GetTodayAsync(CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var salesAvailable = tenant.IsModuleEnabled("Sales");
        var accountingAvailable = tenant.IsModuleEnabled("Accounting");
        var inventoryAvailable = tenant.IsModuleEnabled("Inventory");
        var salesSummary = salesAvailable
            ? await sales.GetAsync(date, date, null, ct)
            : null;
        var expenseTotal = accountingAvailable
            ? await expenses.GetTotalForDateAsync(date, ct)
            : 0m;
        var lowStockCount = inventoryAvailable
            ? (await lowStock.GetAsync(null, 1, 1, ct)).TotalItems
            : 0L;
        return new DashboardSummaryDto(date, "UTC",
            salesSummary?.Revenue ?? 0m, salesSummary?.GrossProfit ?? 0m,
            salesSummary?.TransactionCount ?? 0L, salesSummary?.ItemsSold ?? 0L,
            salesSummary?.AverageTransactionValue ?? 0m, expenseTotal,
            lowStockCount, salesAvailable, accountingAvailable, inventoryAvailable);
    }
}
