using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Reporting.UseCases.ProductPerformance;
using Simplia.Sales.Infrastructure.Data;

namespace Simplia.Api.Modules;

public sealed class ProductPerformanceReportService(SalesDbContext sales,
    InventoryDbContext inventory, ITenantContext tenant)
    : IProductPerformanceReportService
{
    // Allocate each sale's stored Total across its lines. Truncate each share to
    // four places and give the remainder to the last line so variant revenues
    // reconcile exactly to the completed sale, including sale-level discounts.
    private const string MetricsCte = """
        WITH SoldLines AS (
            SELECT l.SaleId, l.ProductVariantId, l.Quantity, l.LineTotal,
                   s.Total,
                   SUM(l.LineTotal) OVER (PARTITION BY l.SaleId) AS LineSum,
                   ROW_NUMBER() OVER (PARTITION BY l.SaleId ORDER BY l.Id) AS LineNumber,
                   COUNT_BIG(*) OVER (PARTITION BY l.SaleId) AS LineCount
            FROM [sales].[SaleLines] AS l
            INNER JOIN [sales].[Sales] AS s ON s.Id = l.SaleId
            WHERE s.SaleDate >= @from AND s.SaleDate <= @to
              AND s.Status IN ('Completed', 'PartiallyReturned', 'Returned')
        ), Shares AS (
            SELECT *, CAST(CASE WHEN LineSum = 0 THEN 0
                 ELSE ROUND(Total * LineTotal / LineSum, 4, 1)
                 END AS decimal(18,4)) AS TruncatedRevenue
            FROM SoldLines
        ), AllocatedSales AS (
            SELECT ProductVariantId, Quantity,
                   TruncatedRevenue + CASE WHEN LineNumber = LineCount
                       THEN Total - SUM(TruncatedRevenue) OVER (PARTITION BY SaleId)
                       ELSE 0 END AS Revenue
            FROM Shares
        ), SoldByVariant AS (
            SELECT ProductVariantId,
                   SUM(CAST(Quantity AS bigint)) AS GrossQuantitySold,
                   SUM(Revenue) AS GrossRevenue
            FROM AllocatedSales GROUP BY ProductVariantId
        ), ReturnedByVariant AS (
            SELECT l.ProductVariantId,
                   SUM(CAST(l.Quantity AS bigint)) AS ReturnedQuantity,
                   SUM(l.RefundAmount) AS RefundedRevenue
            FROM [sales].[SaleReturnLines] AS l
            INNER JOIN [sales].[SaleReturns] AS r ON r.Id = l.SaleReturnId
            WHERE r.ReturnDate >= @from AND r.ReturnDate <= @to
            GROUP BY l.ProductVariantId
        ), VariantActivity AS (
            SELECT COALESCE(s.ProductVariantId, r.ProductVariantId) AS ProductVariantId,
                   COALESCE(s.GrossQuantitySold, 0) AS GrossQuantitySold,
                   COALESCE(r.ReturnedQuantity, 0) AS ReturnedQuantity,
                   COALESCE(s.GrossRevenue, 0) AS GrossRevenue,
                   COALESCE(r.RefundedRevenue, 0) AS RefundedRevenue
            FROM SoldByVariant AS s
            FULL OUTER JOIN ReturnedByVariant AS r
                ON r.ProductVariantId = s.ProductVariantId
        )
        """;

    private const string ProductSql = MetricsCte + """
        SELECT p.Id, p.Name,
               SUM(a.GrossQuantitySold) AS GrossQuantitySold,
               SUM(a.ReturnedQuantity) AS ReturnedQuantity,
               SUM(a.GrossRevenue) AS GrossRevenue,
               SUM(a.RefundedRevenue) AS RefundedRevenue
        FROM VariantActivity AS a
        INNER JOIN [inventory].[ProductVariants] AS v ON v.Id = a.ProductVariantId
        INNER JOIN [inventory].[Products] AS p ON p.Id = v.ProductId
        GROUP BY p.Id, p.Name
        ORDER BY CASE WHEN @rankBy = 1
                 THEN SUM(a.GrossQuantitySold) - SUM(a.ReturnedQuantity) END DESC,
                 CASE WHEN @rankBy = 2
                 THEN SUM(a.GrossRevenue) - SUM(a.RefundedRevenue) END DESC,
                 p.Id
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;

    private const string VariantSql = MetricsCte + """
        SELECT v.Id, v.ProductId, v.Sku, v.Barcode,
               COALESCE(a.GrossQuantitySold, 0) AS GrossQuantitySold,
               COALESCE(a.ReturnedQuantity, 0) AS ReturnedQuantity,
               COALESCE(a.GrossRevenue, 0) AS GrossRevenue,
               COALESCE(a.RefundedRevenue, 0) AS RefundedRevenue
        FROM [inventory].[ProductVariants] AS v
        LEFT JOIN VariantActivity AS a ON a.ProductVariantId = v.Id
        WHERE v.ProductId = @productId
        ORDER BY v.Sku, v.Id
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;

    public async Task<IReadOnlyList<ProductPerformanceDto>> RankProductsAsync(
        DateOnly fromDate, DateOnly toDate, ProductPerformanceRankBy rankBy,
        int page, int pageSize, CancellationToken ct)
    {
        Validate(fromDate, toDate, page, pageSize);
        if (!Enum.IsDefined(rankBy)) throw new ArgumentOutOfRangeException(nameof(rankBy));
        return await ReadAsync(ProductSql, fromDate, toDate, page, pageSize,
            command => command.Parameters.Add(new SqlParameter("@rankBy", SqlDbType.Int)
                { Value = (int)rankBy }),
            reader =>
            {
                var grossQuantity = reader.GetInt64(2);
                var returnedQuantity = reader.GetInt64(3);
                var grossRevenue = reader.GetDecimal(4);
                var refundedRevenue = reader.GetDecimal(5);
                return new ProductPerformanceDto(reader.GetGuid(0), reader.GetString(1),
                    grossQuantity, returnedQuantity, grossQuantity - returnedQuantity,
                    grossRevenue, refundedRevenue, grossRevenue - refundedRevenue);
            }, ct);
    }

    public async Task<IReadOnlyList<VariantPerformanceDto>> GetVariantsAsync(
        Guid productId, DateOnly fromDate, DateOnly toDate, int page,
        int pageSize, CancellationToken ct)
    {
        Validate(fromDate, toDate, page, pageSize);
        if (productId == Guid.Empty)
            throw new ArgumentException("Product is required.", nameof(productId));
        EnsureModules();
        if (!await inventory.Products.AsNoTracking().AnyAsync(x => x.Id == productId, ct))
            throw new NotFoundException("Product", productId);
        return await ReadAsync(VariantSql, fromDate, toDate, page, pageSize,
            command => command.Parameters.Add(new SqlParameter("@productId", SqlDbType.UniqueIdentifier)
                { Value = productId }),
            reader =>
            {
                var grossQuantity = reader.GetInt64(4);
                var returnedQuantity = reader.GetInt64(5);
                var grossRevenue = reader.GetDecimal(6);
                var refundedRevenue = reader.GetDecimal(7);
                return new VariantPerformanceDto(reader.GetGuid(0), reader.GetGuid(1),
                    reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                    grossQuantity, returnedQuantity, grossQuantity - returnedQuantity,
                    grossRevenue, refundedRevenue, grossRevenue - refundedRevenue);
            }, ct);
    }

    private async Task<IReadOnlyList<T>> ReadAsync<T>(string sql,
        DateOnly fromDate, DateOnly toDate, int page, int pageSize,
        Action<SqlCommand> addParameters, Func<SqlDataReader, T> map,
        CancellationToken ct)
    {
        EnsureModules();
        var start = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue,
            DateTimeKind.Utc));
        var end = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        await sales.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = (SqlCommand)sales.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@from", SqlDbType.DateTimeOffset)
                { Value = start });
            command.Parameters.Add(new SqlParameter("@to", SqlDbType.DateTimeOffset)
                { Value = end });
            command.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int)
                { Value = (page - 1) * pageSize });
            command.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int)
                { Value = pageSize });
            addParameters(command);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<T>();
            while (await reader.ReadAsync(ct)) rows.Add(map(reader));
            return rows;
        }
        finally
        {
            await sales.Database.CloseConnectionAsync();
        }
    }

    private static void Validate(DateOnly fromDate, DateOnly toDate,
        int page, int pageSize)
    {
        if (fromDate == default || toDate == default || fromDate > toDate)
            throw new ArgumentException("A valid date range is required.");
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
    }

    private void EnsureModules()
    {
        if (!tenant.IsModuleEnabled("Sales") || !tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Sales and Inventory must be enabled for product performance reports.");
    }
}
