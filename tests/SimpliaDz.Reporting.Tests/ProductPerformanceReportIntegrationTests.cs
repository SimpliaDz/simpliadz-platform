using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.ValueObjects;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Reporting.UseCases.ProductPerformance;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Reporting.Tests;

public sealed class ProductPerformanceReportIntegrationTests
{
    [Fact]
    public async Task Product_rankings_and_variant_details_use_period_sales_and_returns()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzPerformanceTest_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnection)
            { InitialCatalog = name }.ConnectionString;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(baseConnection)
                     { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{name}]", master);
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await using var inventory = NewInventory(connection);
            await using var sales = NewSales(connection);
            await inventory.Database.MigrateAsync();
            await sales.Database.MigrateAsync();
            var productA = new Product("Performance A");
            var productB = new Product("Performance B");
            var a1 = new ProductVariant(productA.Id, new Sku("PERF-A1"), 0);
            var a2 = new ProductVariant(productA.Id, new Sku("PERF-A2"), 0);
            var a3 = new ProductVariant(productA.Id, new Sku("PERF-A3"), 0);
            var b1 = new ProductVariant(productB.Id, new Sku("PERF-B1"), 0);
            inventory.Products.AddRange(productA, productB);
            inventory.ProductVariants.AddRange(a1, a2, a3, b1);
            await inventory.SaveChangesAsync();

            var actor = Guid.NewGuid();
            var location = Guid.NewGuid();
            var prices = Guid.NewGuid();
            var saleA = new Sale(location, null, actor, prices);
            saleA.AddLine(a1.Id, 3, 10m, 4m);
            var a2Line = saleA.AddLine(a2.Id, 2, 20m, 7m);
            saleA.ApplySaleDiscount(SaleDiscountType.FixedAmount, 10m);
            saleA.Complete(actor);
            var saleB = new Sale(location, null, actor, prices);
            saleB.AddLine(b1.Id, 7, 5m, 2m);
            saleB.Complete(actor);
            var priorSale = new Sale(location, null, actor, prices);
            var priorLine = priorSale.AddLine(b1.Id, 2, 5m, 2m);
            priorSale.Complete(actor);
            sales.Sales.AddRange(saleA, saleB, priorSale);
            await sales.SaveChangesAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);
            var priorAt = new DateTimeOffset(yesterday.ToDateTime(new TimeOnly(12, 0),
                DateTimeKind.Utc));
            await sales.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [sales].[Sales] SET [SaleDate] = {priorAt}
                WHERE [Id] = {priorSale.Id}
                """);
            await sales.Entry(priorSale).ReloadAsync();
            saleA.RecordReturn(a2Line.Id, 1);
            priorSale.RecordReturn(priorLine.Id, 1);
            sales.SaleReturns.Add(new SaleReturn(Guid.NewGuid(), saleA.Id, location,
                "Size return", actor,
                [new SaleReturnLineItem(a2Line.Id, a2.Id, 1, 17.1428m, false)]));
            sales.SaleReturns.Add(new SaleReturn(Guid.NewGuid(), priorSale.Id, location,
                "Older sale return", actor,
                [new SaleReturnLineItem(priorLine.Id, b1.Id, 1, 5m, false)]));
            await sales.SaveChangesAsync();

            var report = new ProductPerformanceReportService(sales, inventory,
                new TestTenant(connection));
            var byQuantity = await report.RankProductsAsync(today, today,
                ProductPerformanceRankBy.Quantity, 1, 50, CancellationToken.None);
            Assert.Equal([productB.Id, productA.Id], byQuantity.Select(x => x.ProductId));
            var byRevenue = await report.RankProductsAsync(today, today,
                ProductPerformanceRankBy.Revenue, 1, 50, CancellationToken.None);
            Assert.Equal([productA.Id, productB.Id], byRevenue.Select(x => x.ProductId));
            var a = byRevenue[0];
            Assert.Equal(5, a.GrossQuantitySold);
            Assert.Equal(1, a.ReturnedQuantity);
            Assert.Equal(4, a.NetQuantitySold);
            Assert.Equal(60m, a.GrossRevenue);
            Assert.Equal(17.1428m, a.RefundedRevenue);
            Assert.Equal(42.8572m, a.NetRevenue);
            var b = byRevenue[1];
            Assert.Equal(7, b.GrossQuantitySold);
            Assert.Equal(1, b.ReturnedQuantity);
            Assert.Equal(6, b.NetQuantitySold);
            Assert.Equal(30m, b.NetRevenue);

            var variants = await report.GetVariantsAsync(productA.Id, today, today,
                1, 50, CancellationToken.None);
            Assert.Equal(3, variants.Count);
            Assert.Equal(60m, variants.Sum(x => x.GrossRevenue));
            Assert.Contains(variants, x => x.ProductVariantId == a2.Id &&
                x.ReturnedQuantity == 1 && x.NetQuantitySold == 1);
            Assert.Contains(variants, x => x.ProductVariantId == a3.Id &&
                x.GrossQuantitySold == 0 && x.NetRevenue == 0m);
            Assert.Single(await report.RankProductsAsync(today, today,
                ProductPerformanceRankBy.Quantity, 2, 1, CancellationToken.None));
            await Assert.ThrowsAsync<NotFoundException>(() => report.GetVariantsAsync(
                Guid.NewGuid(), today, today, 1, 50, CancellationToken.None));
            Assert.Empty(await report.RankProductsAsync(today.AddDays(1),
                today.AddDays(1), ProductPerformanceRankBy.Quantity, 1, 50,
                CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() =>
                new ProductPerformanceReportService(sales, inventory,
                    new TestTenant(connection, false))
                    .RankProductsAsync(today, today, ProductPerformanceRankBy.Quantity,
                        1, 50, CancellationToken.None));
        }
        finally
        {
            await using var cleanup = NewSales(connection);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static InventoryDbContext NewInventory(string connection) => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options);

    private static SalesDbContext NewSales(string connection) => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales"))
            .Options);

    private sealed class TestTenant(string connection, bool inventoryEnabled = true) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName switch
        {
            "Reporting" or "Sales" => true,
            "Inventory" => inventoryEnabled,
            _ => false
        };
    }
}
