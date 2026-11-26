namespace Simplia.Reporting.UseCases.FinancialDashboard;

public sealed record FinancialDashboardDto(DateOnly FromDate, DateOnly ToDate, Guid? LocationId,
    decimal NetSales, decimal Discounts, decimal Refunds, decimal CostOfGoodsSold,
    decimal GrossProfit, decimal GrossMarginPercent, decimal Expenses, decimal OperatingContribution,
    long TransactionCount, long ItemsSold, decimal AverageTransactionValue,
    IReadOnlyList<FinancialTrendPointDto> DailyTrend,
    IReadOnlyList<PaymentMethodBreakdownDto> PaymentMethods,
    IReadOnlyList<LocationPerformanceDto> Locations, IReadOnlyList<TopProductDto> TopProducts);
public sealed record FinancialTrendPointDto(DateOnly Date, decimal NetSales, decimal GrossProfit, decimal Expenses, long Transactions);
public sealed record PaymentMethodBreakdownDto(string Method, decimal Amount, decimal PercentOfPayments);
public sealed record LocationPerformanceDto(Guid LocationId, string LocationName, decimal NetSales, decimal GrossProfit, decimal Expenses, long Transactions);
public sealed record TopProductDto(Guid ProductId, string ProductName, decimal NetSales, long NetQuantity);
public interface IFinancialDashboardService { Task<FinancialDashboardDto> GetAsync(DateOnly fromDate, DateOnly toDate, Guid? locationId, CancellationToken ct); }
