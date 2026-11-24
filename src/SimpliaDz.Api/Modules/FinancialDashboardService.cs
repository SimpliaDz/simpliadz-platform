using Microsoft.EntityFrameworkCore;
using SimpliaDz.Accounting.Infrastructure.Data;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Reporting.UseCases.FinancialDashboard;
using SimpliaDz.Sales.Infrastructure.Data;

namespace SimpliaDz.Api.Modules;

/// <summary>Read-only dashboard sourced from posted sales, returns and expenses.</summary>
public sealed class FinancialDashboardService(SalesDbContext sales, AccountingDbContext accounting,
    ITenantContext tenant) : IFinancialDashboardService
{
    public async Task<FinancialDashboardDto> GetAsync(DateOnly from, DateOnly to, Guid? locationId, CancellationToken ct)
    {
        if (from == default || to == default || from > to) throw new ArgumentException("A valid date range is required.");
        if (!tenant.IsModuleEnabled("Sales")) throw new ConflictException("Sales must be enabled for the financial dashboard.");
        var saleRows = await sales.Sales.AsNoTracking().Where(x => x.SaleDate >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && x.SaleDate <= to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) && (x.Status == SimpliaDz.Sales.Core.Entities.SaleStatus.Completed || x.Status == SimpliaDz.Sales.Core.Entities.SaleStatus.PartiallyReturned || x.Status == SimpliaDz.Sales.Core.Entities.SaleStatus.Returned) && (!locationId.HasValue || x.LocationId == locationId)).Select(x => new { x.Id, x.LocationId, x.SaleDate, x.Total, x.Subtotal }).ToListAsync(ct);
        var saleIds = saleRows.Select(x => x.Id).ToList();
        var lines = await sales.SaleLines.AsNoTracking().Where(x => saleIds.Contains(x.SaleId)).Select(x => new { x.SaleId, x.ProductVariantId, x.Quantity, x.LineTotal, x.UnitCostSnapshot }).ToListAsync(ct);
        var returns = await sales.SaleReturns.AsNoTracking().Where(x => x.ReturnDate >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && x.ReturnDate <= to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) && (!locationId.HasValue || x.LocationId == locationId)).Select(x => new { x.Id, x.LocationId, x.ReturnDate }).ToListAsync(ct);
        var returnIds = returns.Select(x => x.Id).ToList();
        var returnLines = await sales.SaleReturnLines.AsNoTracking().Where(x => returnIds.Contains(x.SaleReturnId)).Select(x => new { x.SaleReturnId, x.OriginalSaleLineId, x.ProductVariantId, x.Quantity, x.RefundAmount }).ToListAsync(ct);
        var expenses = tenant.IsModuleEnabled("Accounting") ? await accounting.Expenses.AsNoTracking().Where(x => x.ExpenseDate >= from && x.ExpenseDate <= to && (!locationId.HasValue || x.LocationId == locationId)).Select(x => new { x.ExpenseDate, x.LocationId, x.Amount }).ToListAsync(ct) : [];
        var locations = await sales.Database.SqlQueryRaw<LocationRow>("SELECT Id, Name FROM [inventory].[Locations]").ToListAsync(ct);
        var saleLines = lines.GroupBy(x => x.SaleId).ToDictionary(x => x.Key, x => x.ToList());
        var originalLineCosts = await sales.SaleLines.AsNoTracking().Where(x => returnLines.Select(r => r.OriginalSaleLineId).Contains(x.Id)).Select(x => new { x.Id, x.UnitCostSnapshot }).ToDictionaryAsync(x => x.Id, x => x.UnitCostSnapshot, ct);
        var refunds = returnLines.Sum(x => x.RefundAmount); var returnedCost = returnLines.Sum(x => x.Quantity * originalLineCosts.GetValueOrDefault(x.OriginalSaleLineId));
        var netSales = saleRows.Sum(x => x.Total) - refunds; var cost = lines.Sum(x => x.UnitCostSnapshot * x.Quantity) - returnedCost;
        var discounts = saleRows.Sum(x => x.Subtotal - x.Total); var grossProfit = netSales - cost;
        var returnedByVariant = returnLines.GroupBy(x => x.ProductVariantId).ToDictionary(x => x.Key, x => x.Sum(y => (long)y.Quantity));
        var productNames = await sales.Database.SqlQueryRaw<ProductRow>("SELECT v.Id AS VariantId, p.Id AS ProductId, p.Name AS ProductName FROM [inventory].[ProductVariants] v INNER JOIN [inventory].[Products] p ON p.Id=v.ProductId").ToListAsync(ct);
        var productByVariant = productNames.ToDictionary(x => x.VariantId);
        var returnedRevenueByVariant = returnLines.GroupBy(x => x.ProductVariantId).ToDictionary(x => x.Key, x => x.Sum(y => y.RefundAmount));
        var topProducts = lines.Where(x => productByVariant.ContainsKey(x.ProductVariantId)).GroupBy(x => productByVariant[x.ProductVariantId].ProductId).Select(x => new TopProductDto(x.Key, x.First() is var line ? productByVariant[line.ProductVariantId].ProductName : string.Empty, x.Sum(y => y.LineTotal) - x.Select(y => y.ProductVariantId).Distinct().Sum(v => returnedRevenueByVariant.GetValueOrDefault(v)), x.Sum(y => (long)y.Quantity) - x.Select(y => y.ProductVariantId).Distinct().Sum(v => returnedByVariant.GetValueOrDefault(v)))).OrderByDescending(x => x.NetSales).Take(5).ToList();
        var expensesByDate = expenses.GroupBy(x => x.ExpenseDate).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var returnDates = returns.ToDictionary(x => x.Id, x => DateOnly.FromDateTime(x.ReturnDate.UtcDateTime));
        var refundsByDate = returnLines.GroupBy(x => returnDates[x.SaleReturnId]).ToDictionary(x => x.Key,
            x => new { Refund = x.Sum(y => y.RefundAmount), Cost = x.Sum(y => y.Quantity * originalLineCosts.GetValueOrDefault(y.OriginalSaleLineId)) });
        var trend = saleRows.GroupBy(x => DateOnly.FromDateTime(x.SaleDate.UtcDateTime)).Select(x => { var date = x.Key; var dayCost = x.Sum(s => saleLines.GetValueOrDefault(s.Id, []).Sum(l => l.UnitCostSnapshot * l.Quantity)); var refund = refundsByDate.GetValueOrDefault(date); var revenue = x.Sum(s => s.Total) - (refund?.Refund ?? 0); return new FinancialTrendPointDto(date, revenue, revenue - dayCost + (refund?.Cost ?? 0), expensesByDate.GetValueOrDefault(date), x.LongCount()); }).OrderBy(x => x.Date).ToList();
        foreach (var day in refundsByDate.Keys.Where(d => trend.All(x => x.Date != d))) trend.Add(new(day, -refundsByDate[day].Refund, -refundsByDate[day].Refund + refundsByDate[day].Cost, expensesByDate.GetValueOrDefault(day), 0));
        foreach (var day in expensesByDate.Keys.Where(d => trend.All(x => x.Date != d))) trend.Add(new(day, 0, 0, expensesByDate[day], 0));
        var expensesByLocation = expenses.Where(x => x.LocationId.HasValue).GroupBy(x => x.LocationId!.Value).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var locationsById = locations.ToDictionary(x => x.Id, x => x.Name);
        var locationPerformance = saleRows.GroupBy(x => x.LocationId).Select(g => { var locLines = g.SelectMany(s => saleLines.GetValueOrDefault(s.Id, [])).ToList(); var locReturns = returns.Where(r => r.LocationId == g.Key).SelectMany(r => returnLines.Where(l => l.SaleReturnId == r.Id)).ToList(); var locCost = locLines.Sum(l => l.UnitCostSnapshot * l.Quantity) - locReturns.Sum(l => l.Quantity * originalLineCosts.GetValueOrDefault(l.OriginalSaleLineId)); var locSales = g.Sum(s => s.Total) - locReturns.Sum(l => l.RefundAmount); return new LocationPerformanceDto(g.Key, locationsById.GetValueOrDefault(g.Key, "Unknown location"), locSales, locSales - locCost, expensesByLocation.GetValueOrDefault(g.Key), g.LongCount()); }).OrderByDescending(x => x.NetSales).ToList();
        var payments = await sales.Payments.AsNoTracking().Where(p => saleIds.Contains(p.SaleId)).GroupBy(x => x.PaymentMethod).Select(x => new { x.Key, Amount = x.Sum(y => y.Amount) }).ToListAsync(ct); var paymentTotal = payments.Sum(x => x.Amount);
        return new(from, to, locationId, netSales, discounts, refunds, cost, grossProfit, netSales == 0 ? 0 : decimal.Round(grossProfit * 100 / netSales, 2), expenses.Sum(x => x.Amount), grossProfit - expenses.Sum(x => x.Amount), saleRows.Count, lines.Sum(x => (long)x.Quantity) - returnLines.Sum(x => (long)x.Quantity), saleRows.Count == 0 ? 0 : decimal.Round(netSales / saleRows.Count, 2), trend.OrderBy(x => x.Date).ToList(), payments.Select(x => new PaymentMethodBreakdownDto(x.Key.ToString(), x.Amount, paymentTotal == 0 ? 0 : decimal.Round(x.Amount * 100 / paymentTotal, 2))).ToList(), locationPerformance, topProducts);
    }
    private sealed class LocationRow { public Guid Id { get; init; } public string Name { get; init; } = string.Empty; }
    private sealed class ProductRow { public Guid VariantId { get; init; } public Guid ProductId { get; init; } public string ProductName { get; init; } = string.Empty; }
}
