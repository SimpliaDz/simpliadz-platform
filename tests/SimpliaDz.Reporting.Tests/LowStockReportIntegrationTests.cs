using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.ValueObjects;
using SimpliaDz.Inventory.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Reporting.Tests;

public sealed class LowStockReportIntegrationTests
{
    [Fact]
    public async Task Report_includes_at_threshold_and_zero_stock_by_location()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzLowStockReportTest_{Guid.NewGuid():N}";
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
            await inventory.Database.MigrateAsync();
            var product = new Product("Report product");
            var atPoint = new ProductVariant(product.Id, new Sku("LOW-AT"), 0);
            var zero = new ProductVariant(product.Id, new Sku("LOW-ZERO"), 0);
            var above = new ProductVariant(product.Id, new Sku("LOW-ABOVE"), 0);
            var store = new InventoryLocation("Store", "STORE");
            var warehouse = new InventoryLocation("Warehouse", "WH", LocationType.Warehouse);
            inventory.Products.Add(product);
            inventory.ProductVariants.AddRange(atPoint, zero, above);
            inventory.Locations.AddRange(store, warehouse);
            inventory.InventoryPolicies.AddRange(
                new InventoryPolicy(atPoint.Id, store.Id, 5, null),
                new InventoryPolicy(zero.Id, store.Id, 0, null),
                new InventoryPolicy(above.Id, store.Id, 2, null),
                new InventoryPolicy(atPoint.Id, warehouse.Id, 6, null));
            var atBalance = new StockBalance(atPoint.Id, store.Id);
            atBalance.ApplyDelta(5);
            var aboveBalance = new StockBalance(above.Id, store.Id);
            aboveBalance.ApplyDelta(3);
            inventory.StockBalances.AddRange(atBalance, aboveBalance);
            inventory.StockMovements.AddRange(
                new StockMovement(atPoint.Id, store.Id, StockMovementType.OpeningBalance, 5, "tester"),
                new StockMovement(above.Id, store.Id, StockMovementType.OpeningBalance, 3, "tester"));
            await inventory.SaveChangesAsync();

            var report = new LowStockReportService(inventory, new TestTenant(connection));
            var all = await report.GetAsync(null, 1, 50, CancellationToken.None);
            Assert.Equal(3, all.TotalItems);
            Assert.DoesNotContain(all.Items, x => x.ProductVariantId == above.Id);
            Assert.Contains(all.Items, x => x.ProductVariantId == atPoint.Id &&
                x.LocationId == store.Id && x.QuantityOnHand == 5 &&
                x.ReorderPoint == 5 && !x.IsOutOfStock);
            Assert.Contains(all.Items, x => x.ProductVariantId == zero.Id &&
                x.LocationId == store.Id && x.QuantityOnHand == 0 &&
                x.ReorderPoint == 0 && x.IsOutOfStock);
            Assert.Contains(all.Items, x => x.ProductVariantId == atPoint.Id &&
                x.LocationId == warehouse.Id && x.QuantityOnHand == 0 &&
                x.IsOutOfStock);

            var storeOnly = await report.GetAsync(store.Id, 1, 1, CancellationToken.None);
            Assert.Equal(2, storeOnly.TotalItems);
            Assert.Single(storeOnly.Items);
            Assert.Equal(store.Id, storeOnly.Items[0].LocationId);
            await Assert.ThrowsAsync<NotFoundException>(() => report.GetAsync(
                Guid.NewGuid(), 1, 50, CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() =>
                new LowStockReportService(inventory, new TestTenant(connection, false))
                    .GetAsync(null, 1, 50, CancellationToken.None));
        }
        finally
        {
            await using var cleanup = NewInventory(connection);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static InventoryDbContext NewInventory(string connection) => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options);

    private sealed class TestTenant(string connection, bool inventoryEnabled = true) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName switch
        {
            "Reporting" => true,
            "Inventory" => inventoryEnabled,
            _ => false
        };
    }
}
