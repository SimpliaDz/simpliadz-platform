using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Reporting.UseCases.SalesSummary;
using SimpliaDz.Sales.Infrastructure.Data;

namespace SimpliaDz.Api.Modules;

public sealed class SalesSummaryReportService(SalesDbContext sales,
    ITenantContext tenant) : ISalesSummaryReportService
{
    public async Task<SalesSummaryDto> GetAsync(DateOnly fromDate,
        DateOnly toDate, Guid? locationId, CancellationToken ct)
    {
        if (fromDate == default || toDate == default || fromDate > toDate)
            throw new ArgumentException("A valid date range is required.");
        if (!tenant.IsModuleEnabled("Sales"))
            throw new ConflictException("Sales must be enabled for the sales summary report.");

        var start = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue,
            DateTimeKind.Utc));
        var end = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        await sales.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = (SqlCommand)sales.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT COALESCE(SUM(x.Revenue), 0) AS Revenue,
                       COALESCE(SUM(x.CostOfGoodsSold), 0) AS CostOfGoodsSold,
                       COUNT_BIG(*) AS TransactionCount,
                       COALESCE(SUM(x.ItemsSold), 0) AS ItemsSold
                FROM (
                    SELECT s.Id, s.Total AS Revenue,
                           SUM(l.UnitCostSnapshot * l.Quantity) AS CostOfGoodsSold,
                           SUM(CAST(l.Quantity AS bigint)) AS ItemsSold
                    FROM [sales].[Sales] AS s
                    INNER JOIN [sales].[SaleLines] AS l ON l.SaleId = s.Id
                    WHERE s.SaleDate >= @from AND s.SaleDate <= @to
                      AND s.Status IN ('Completed', 'PartiallyReturned', 'Returned')
                      AND (@locationId IS NULL OR s.LocationId = @locationId)
                    GROUP BY s.Id, s.Total
                ) AS x;
                """;
            command.Parameters.Add(new SqlParameter("@from", SqlDbType.DateTimeOffset)
                { Value = start });
            command.Parameters.Add(new SqlParameter("@to", SqlDbType.DateTimeOffset)
                { Value = end });
            command.Parameters.Add(new SqlParameter("@locationId", SqlDbType.UniqueIdentifier)
                { Value = locationId ?? (object)DBNull.Value });
            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            var revenue = reader.GetDecimal(0);
            var cost = reader.GetDecimal(1);
            var transactions = reader.GetInt64(2);
            var items = reader.GetInt64(3);
            var average = transactions == 0 ? 0m :
                decimal.Round(revenue / transactions, 4, MidpointRounding.AwayFromZero);
            return new SalesSummaryDto(fromDate, toDate, revenue, cost,
                revenue - cost, transactions, items, average);
        }
        finally
        {
            await sales.Database.CloseConnectionAsync();
        }
    }
}
