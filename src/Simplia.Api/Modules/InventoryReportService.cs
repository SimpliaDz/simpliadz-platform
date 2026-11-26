using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Reporting.UseCases.Inventory;

namespace Simplia.Api.Modules;

public sealed class InventoryReportService(InventoryDbContext inventory, ITenantContext tenant)
    : IInventoryReportService
{
    private const string SqlStart = """
        SELECT v.Id AS ProductVariantId, v.ProductId, p.Name AS ProductName,
               v.Sku, v.Barcode,
               COALESCE(SUM(CAST(b.QuantityOnHand AS bigint)), 0) AS QuantityOnHand
        INTO #Stock
        FROM [inventory].[ProductVariants] AS v
        INNER JOIN [inventory].[Products] AS p ON p.Id = v.ProductId
        LEFT JOIN [inventory].[StockItems] AS b ON b.VariantId = v.Id
            AND (@locationId IS NULL OR b.LocationId = @locationId)
        GROUP BY v.Id, v.ProductId, p.Name, v.Sku, v.Barcode;

        SELECT s.*, c.UnitCost,
               CASE WHEN s.QuantityOnHand = 0 THEN CAST(0 AS decimal(38,4))
                    WHEN c.UnitCost IS NULL THEN NULL
                    ELSE s.QuantityOnHand * c.UnitCost END AS StockValue
        INTO #Valued
        FROM #Stock AS s
        """;

    private const string CostJoin = """
        OUTER APPLY (
            SELECT TOP (1) l.UnitCost
            FROM [purchasing].[GoodsReceiptLines] AS l
            INNER JOIN [purchasing].[GoodsReceipts] AS r ON r.Id = l.GoodsReceiptId
            WHERE l.ProductVariantId = s.ProductVariantId
            ORDER BY r.ReceivedAt DESC, l.Id DESC
        ) AS c
        """;

    private const string NoCostJoin = """
        OUTER APPLY (SELECT CAST(NULL AS decimal(18,4)) AS UnitCost) AS c
        """;

    private const string SqlEnd = """
        SELECT COUNT_BIG(*), COALESCE(SUM(QuantityOnHand), 0),
               COALESCE(SUM(COALESCE(StockValue, 0)), 0),
               COALESCE(SUM(CASE WHEN StockValue IS NULL THEN QuantityOnHand ELSE 0 END), 0)
        FROM #Valued
        WHERE @includeZero = 1 OR QuantityOnHand <> 0;

        SELECT ProductVariantId, ProductId, ProductName, Sku, Barcode,
               QuantityOnHand, UnitCost, StockValue
        FROM #Valued
        WHERE @includeZero = 1 OR QuantityOnHand <> 0
        ORDER BY ProductName, Sku, ProductVariantId
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;

    public async Task<InventoryReportDto> GetAsync(Guid? locationId,
        bool includeZeroStock, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (locationId == Guid.Empty)
            throw new ArgumentException("Location is required when supplied.", nameof(locationId));
        if (!tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Inventory must be enabled for inventory reports.");
        if (locationId.HasValue && !await inventory.Locations.AsNoTracking()
                .AnyAsync(x => x.Id == locationId.Value, ct))
            throw new NotFoundException("Location", locationId.Value);

        await inventory.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = (SqlCommand)inventory.Database.GetDbConnection().CreateCommand();
            command.CommandText = SqlStart + "\n" +
                (tenant.IsModuleEnabled("Purchasing") ? CostJoin : NoCostJoin) +
                "\n" + SqlEnd;
            command.Parameters.Add(new SqlParameter("@locationId", SqlDbType.UniqueIdentifier)
                { Value = locationId.HasValue ? locationId.Value : DBNull.Value });
            command.Parameters.Add(new SqlParameter("@includeZero", SqlDbType.Bit)
                { Value = includeZeroStock });
            command.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int)
                { Value = (page - 1) * pageSize });
            command.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int)
                { Value = pageSize });
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Inventory report summary was not returned.");
            var totalVariants = reader.GetInt64(0);
            var totalQuantity = reader.GetInt64(1);
            var knownValue = reader.GetDecimal(2);
            var unvaluedQuantity = reader.GetInt64(3);
            await reader.NextResultAsync(ct);
            var rows = new List<InventoryReportRow>();
            while (await reader.ReadAsync(ct))
                rows.Add(new InventoryReportRow(reader.GetGuid(0), reader.GetGuid(1),
                    reader.GetString(2), reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetInt64(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    reader.IsDBNull(7) ? null : reader.GetDecimal(7)));
            return new InventoryReportDto(locationId, includeZeroStock, totalVariants,
                totalQuantity, knownValue, unvaluedQuantity,
                unvaluedQuantity == 0 ? knownValue : null, rows);
        }
        finally
        {
            await inventory.Database.CloseConnectionAsync();
        }
    }
}
