using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.ValueObjects;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Purchasing.Core.Entities;
using SimpliaDz.Purchasing.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Reporting.Tests;

public sealed class InventoryReportIntegrationTests
{
    [Fact]
    public async Task Report_filters_location_and_values_stock_using_latest_receipt_cost()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzInventoryReportTest_{Guid.NewGuid():N}";
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
            await using var purchasing = NewPurchasing(connection);
            await inventory.Database.MigrateAsync();
            await purchasing.Database.MigrateAsync();
            var product = new Product("Report product");
            var valued = new ProductVariant(product.Id, new Sku("REPORT-A"), 0);
            var unvalued = new ProductVariant(product.Id, new Sku("REPORT-B"), 0);
            var zero = new ProductVariant(product.Id, new Sku("REPORT-C"), 0);
            var store = new InventoryLocation("Store", "STORE");
            var warehouse = new InventoryLocation("Warehouse", "WH", LocationType.Warehouse);
            inventory.Products.Add(product);
            inventory.ProductVariants.AddRange(valued, unvalued, zero);
            inventory.Locations.AddRange(store, warehouse);
            var storeBalance = new StockBalance(valued.Id, store.Id);
            storeBalance.ApplyDelta(3);
            var warehouseBalance = new StockBalance(valued.Id, warehouse.Id);
            warehouseBalance.ApplyDelta(2);
            var unvaluedBalance = new StockBalance(unvalued.Id, store.Id);
            unvaluedBalance.ApplyDelta(4);
            inventory.StockBalances.AddRange(storeBalance, warehouseBalance, unvaluedBalance);
            inventory.StockMovements.AddRange(
                new StockMovement(valued.Id, store.Id, StockMovementType.OpeningBalance, 3, "tester"),
                new StockMovement(valued.Id, warehouse.Id, StockMovementType.OpeningBalance, 2, "tester"),
                new StockMovement(unvalued.Id, store.Id, StockMovementType.OpeningBalance, 4, "tester"));
            await inventory.SaveChangesAsync();

            var supplier = new Supplier("Supplier");
            var order = new PurchaseOrder(supplier.Id, store.Id, "tester");
            var line = order.AddLine(valued.Id, 5, 12m);
            order.PlaceOrder();
            purchasing.Suppliers.Add(supplier);
            purchasing.PurchaseOrders.Add(order);
            await purchasing.SaveChangesAsync();
            var receipt = new GoodsReceipt(Guid.NewGuid(), order.Id, store.Id, "tester",
                [new GoodsReceiptLineItem(line.Id, valued.Id, 5, 12m)]);
            purchasing.GoodsReceipts.Add(receipt);
            await purchasing.SaveChangesAsync();

            var report = new InventoryReportService(inventory, new TestTenant(connection));
            var all = await report.GetAsync(null, false, 1, 50, CancellationToken.None);
            Assert.Equal(2, all.TotalVariants);
            Assert.Equal(9, all.TotalQuantityOnHand);
            Assert.Equal(60m, all.KnownStockValue);
            Assert.Equal(4, all.UnvaluedQuantity);
            Assert.Null(all.TotalStockValue);
            Assert.Contains(all.Items, x => x.ProductVariantId == valued.Id &&
                x.QuantityOnHand == 5 && x.UnitCost == 12m && x.StockValue == 60m);
            Assert.Contains(all.Items, x => x.ProductVariantId == unvalued.Id &&
                x.QuantityOnHand == 4 && x.StockValue is null);

            var filtered = await report.GetAsync(warehouse.Id, false, 1, 50,
                CancellationToken.None);
            Assert.Single(filtered.Items);
            Assert.Equal(2, filtered.TotalQuantityOnHand);
            Assert.Equal(24m, filtered.TotalStockValue);
            var withZero = await report.GetAsync(warehouse.Id, true, 1, 1,
                CancellationToken.None);
            Assert.Equal(3, withZero.TotalVariants);
            Assert.Single(withZero.Items);
            Assert.Equal(24m, withZero.TotalStockValue);
            Assert.Equal(0, withZero.UnvaluedQuantity);
            var withoutPurchasing = await new InventoryReportService(inventory,
                new TestTenant(connection, purchasingEnabled: false))
                .GetAsync(warehouse.Id, false, 1, 50, CancellationToken.None);
            Assert.Equal(2, withoutPurchasing.UnvaluedQuantity);
            Assert.Null(withoutPurchasing.TotalStockValue);
            await Assert.ThrowsAsync<NotFoundException>(() => report.GetAsync(
                Guid.NewGuid(), false, 1, 50, CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() =>
                new InventoryReportService(inventory, new TestTenant(connection, false))
                    .GetAsync(null, false, 1, 50, CancellationToken.None));
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

    private static PurchasingDbContext NewPurchasing(string connection) => new(
        new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "purchasing"))
            .Options);

    private sealed class TestTenant(string connection, bool inventoryEnabled = true,
        bool purchasingEnabled = true) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName switch
        {
            "Reporting" => true,
            "Inventory" => inventoryEnabled,
            "Purchasing" => purchasingEnabled,
            _ => false
        };
    }
}
