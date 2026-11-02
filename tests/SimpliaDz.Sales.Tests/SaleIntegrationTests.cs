using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.Exceptions;
using SimpliaDz.Inventory.Core.ValueObjects;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Purchasing.Core.Entities;
using SimpliaDz.Purchasing.Infrastructure.Data;
using SimpliaDz.Purchasing.UseCases.Services;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.Infrastructure.Services;
using Xunit;

namespace SimpliaDz.Sales.Tests;

public sealed class SaleIntegrationTests
{
    [Fact]
    public async Task Sale_snapshots_price_and_cost_and_completes_atomically_with_stock()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzSaleTest_{Guid.NewGuid():N}";
        var connectionString = ForDatabase(baseConnection, name);
        await using (var master = new SqlConnection(ForDatabase(baseConnection, "master")))
        {
            await master.OpenAsync();
            await using var create = new SqlCommand($"CREATE DATABASE [{name}]", master);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            await using var inventory = NewInventory(connectionString);
            await using var purchasing = NewPurchasing(connectionString);
            await using var sales = NewSales(connectionString);
            await inventory.Database.MigrateAsync();
            await purchasing.Database.MigrateAsync();
            await sales.Database.MigrateAsync();
            var tenant = new TestTenant(connectionString);

            var product = new Product("Sale test product");
            var variant = new ProductVariant(product.Id, new Sku("SALE-TEST"), 0,
                barcode: "SALE-BARCODE");
            var location = new InventoryLocation("Sale test store");
            var priceList = new PriceList("Sale test prices", "DZD", PriceListType.Retail);
            priceList.SetPrice(variant.Id, 12m);
            inventory.Products.Add(product);
            inventory.ProductVariants.Add(variant);
            inventory.Locations.Add(location);
            inventory.PriceLists.Add(priceList);
            await inventory.SaveChangesAsync();

            var supplier = new Supplier("Sale test supplier");
            var order = new PurchaseOrder(supplier.Id, location.Id, "buyer");
            var orderLine = order.AddLine(variant.Id, 10, 6.5m);
            order.PlaceOrder();
            purchasing.Suppliers.Add(supplier);
            purchasing.PurchaseOrders.Add(order);
            await purchasing.SaveChangesAsync();
            purchasing.ChangeTracker.Clear();
            await new GoodsReceiptService(purchasing, tenant).ReceiveAsync(Guid.NewGuid(),
                order.Id, [new ReceiveGoodsLine(orderLine.Id, 10)], "receiver", CancellationToken.None);
            inventory.ChangeTracker.Clear();

            var customer = new Customer("Sale test customer", null, null, null,
                CustomerType.Retail, null);
            sales.Customers.Add(customer);
            await sales.SaveChangesAsync();
            var cashier = Guid.NewGuid();
            var service = new SaleService(sales, inventory, purchasing, tenant);
            var first = await service.CreateDraftAsync(location.Id, customer.Id, cashier,
                priceList.Id, CancellationToken.None);
            first = await service.AddLineAsync(first.Id, "SALE-TEST", null, 2, 1m,
                CancellationToken.None);
            Assert.Equal(12m, first.Lines.Single().UnitPrice);
            Assert.Equal(6.5m, first.Lines.Single().UnitCostSnapshot);
            first = await service.SetDiscountAsync(first.Id, 1m, CancellationToken.None);
            Assert.Equal(24m, first.Subtotal);
            Assert.Equal(22m, first.Total);
            Assert.Null(first.SaleNumber);

            first = await service.CompleteAsync(first.Id, cashier, CancellationToken.None);
            Assert.Equal("Completed", first.Status);
            Assert.StartsWith("SALE-", first.SaleNumber);
            await Assert.ThrowsAsync<ConflictException>(() => service.CompleteAsync(first.Id,
                cashier, CancellationToken.None));
            sales.ChangeTracker.Clear();
            inventory.ChangeTracker.Clear();
            Assert.Equal(8, (await inventory.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.Contains(await inventory.StockMovements.ToListAsync(), x =>
                x.MovementType == StockMovementType.Sale && x.ReferenceId == first.Id &&
                x.QuantityDelta == -2);

            await new CustomerService(sales).DeactivateAsync(customer.Id, CancellationToken.None);
            Assert.Equal(customer.Id, (await service.GetByIdAsync(first.Id, CancellationToken.None)).CustomerId);

            var tooLarge = await service.CreateDraftAsync(location.Id, null, cashier,
                priceList.Id, CancellationToken.None);
            tooLarge = await service.AddLineAsync(tooLarge.Id, null, "SALE-BARCODE", 9, 0m,
                CancellationToken.None);
            var tooLargeId = tooLarge.Id;
            await Assert.ThrowsAsync<InsufficientStockException>(() => service.CompleteAsync(
                tooLargeId, cashier, CancellationToken.None));
            Assert.Equal("Draft", (await service.GetByIdAsync(tooLargeId, CancellationToken.None)).Status);

            var failed = await service.CreateDraftAsync(location.Id, null, cashier,
                priceList.Id, CancellationToken.None);
            failed = await service.AddLineAsync(failed.Id, "SALE-TEST", null, 1, 0m,
                CancellationToken.None);
            await inventory.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER inventory.RejectSaleTest ON inventory.StockMovements
                AFTER INSERT AS BEGIN THROW 51002, 'Test sale stock failure', 1; END
                """);
            var failedId = failed.Id;
            await Assert.ThrowsAnyAsync<Exception>(() => service.CompleteAsync(failedId,
                cashier, CancellationToken.None));
            await inventory.Database.ExecuteSqlRawAsync("DROP TRIGGER inventory.RejectSaleTest");
            sales.ChangeTracker.Clear();
            inventory.ChangeTracker.Clear();
            Assert.Equal("Draft", (await service.GetByIdAsync(failedId, CancellationToken.None)).Status);
            Assert.Equal(8, (await inventory.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.DoesNotContain(await inventory.StockMovements.ToListAsync(),
                x => x.MovementType == StockMovementType.Sale && x.ReferenceId == failedId);
        }
        finally
        {
            await using var cleanup = NewSales(connectionString);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static string ForDatabase(string baseConnection, string name) =>
        new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = name }.ConnectionString;

    private static InventoryDbContext NewInventory(string connectionString) => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options);

    private static PurchasingDbContext NewPurchasing(string connectionString) => new(
        new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "purchasing"))
            .Options);

    private static SalesDbContext NewSales(string connectionString) => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales"))
            .Options);

    private sealed class TestTenant(string connectionString) : ITenantContext
    {
        public string TenantId => "integration";
        public string Name => "integration";
        public string ConnectionString => connectionString;
        public bool IsModuleEnabled(string moduleName) =>
            moduleName is "Sales" or "Inventory" or "Purchasing";
    }
}
