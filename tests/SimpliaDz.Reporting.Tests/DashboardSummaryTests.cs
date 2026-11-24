using SimpliaDz.Accounting.UseCases.Services;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Reporting.UseCases.LowStock;
using SimpliaDz.Reporting.UseCases.SalesSummary;
using Xunit;

namespace SimpliaDz.Reporting.Tests;

public sealed class DashboardSummaryTests
{
    [Fact]
    public async Task Today_combines_report_totals_using_one_utc_date()
    {
        var day = new DateTimeOffset(2026, 11, 3, 0, 30, 0, TimeSpan.Zero);
        var sales = new SalesStub();
        var expenses = new ExpenseStub();
        var lowStock = new LowStockStub();
        var dashboard = new DashboardSummaryService(new TestTenant(), sales,
            expenses, lowStock, new FixedTimeProvider(day));

        var result = await dashboard.GetTodayAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 3), result.Date);
        Assert.Equal("UTC", result.TimeZone);
        Assert.Equal(100m, result.Revenue);
        Assert.Equal(40m, result.GrossProfit);
        Assert.Equal(2, result.TransactionCount);
        Assert.Equal(5, result.ItemsSold);
        Assert.Equal(50m, result.AverageBasket);
        Assert.Equal(18m, result.Expenses);
        Assert.Equal(7, result.LowStockCount);
        Assert.True(result.SalesAvailable && result.AccountingAvailable && result.InventoryAvailable);
        Assert.Equal(result.Date, sales.RequestedDate);
        Assert.Equal(result.Date, expenses.RequestedDate);
        Assert.Equal(1, lowStock.Requests);
    }

    [Fact]
    public async Task Disabled_modules_are_marked_unavailable_and_not_queried()
    {
        var dashboard = new DashboardSummaryService(new TestTenant(false),
            new SalesStub(), new ExpenseStub(), new LowStockStub(),
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero)));

        var result = await dashboard.GetTodayAsync(CancellationToken.None);

        Assert.False(result.SalesAvailable || result.AccountingAvailable || result.InventoryAvailable);
        Assert.Equal(0m, result.Revenue);
        Assert.Equal(0m, result.Expenses);
        Assert.Equal(0, result.LowStockCount);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SalesStub : ISalesSummaryReportService
    {
        public DateOnly? RequestedDate { get; private set; }
        public Task<SalesSummaryDto> GetAsync(DateOnly fromDate, DateOnly toDate,
            Guid? locationId, CancellationToken ct)
        {
            RequestedDate = fromDate;
            Assert.Equal(fromDate, toDate);
            Assert.Null(locationId);
            return Task.FromResult(new SalesSummaryDto(fromDate, toDate,
                100m, 60m, 40m, 2, 5, 50m));
        }
    }

    private sealed class ExpenseStub : IExpenseTotalReader
    {
        public DateOnly? RequestedDate { get; private set; }
        public Task<decimal> GetTotalForDateAsync(DateOnly date, CancellationToken ct)
        {
            RequestedDate = date;
            return Task.FromResult(18m);
        }
    }

    private sealed class LowStockStub : ILowStockReportService
    {
        public int Requests { get; private set; }
        public Task<LowStockReportDto> GetAsync(Guid? locationId, int page,
            int pageSize, CancellationToken ct)
        {
            Requests++;
            Assert.Null(locationId);
            Assert.Equal(1, page);
            Assert.Equal(1, pageSize);
            return Task.FromResult(new LowStockReportDto(null, 7, []));
        }
    }

    private sealed class TestTenant(bool enabled = true) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => "test";
        public bool IsModuleEnabled(string moduleName) => enabled;
    }
}
