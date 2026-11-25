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
using SimpliaDz.Sales.UseCases.Services;
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
            var posDraft = await service.CreateDraftAsync(location.Id, null, cashier,
                priceList.Id, CancellationToken.None);
            posDraft = await service.AddLineAsync(posDraft.Id, "SALE-TEST", null, 2, 0m,
                CancellationToken.None, checkStock: true);
            var lineId = posDraft.Lines.Single().Id;
            await Assert.ThrowsAsync<ConflictException>(() => service.SetLineQuantityAsync(
                posDraft.Id, lineId, 11, CancellationToken.None, checkStock: true));
            await Assert.ThrowsAsync<ConflictException>(() => service.AddLineAsync(
                posDraft.Id, "SALE-TEST", null, 9, 0m, CancellationToken.None, checkStock: true));
            var removed = await service.RemoveLineAsync(posDraft.Id, lineId, CancellationToken.None);
            Assert.Empty(removed.Lines);
            Assert.Equal(0m, removed.Total);
            Assert.False(await sales.SaleLines.AsNoTracking().AnyAsync(line => line.Id == lineId));

            var first = await service.CreateDraftAsync(location.Id, customer.Id, cashier,
                priceList.Id, CancellationToken.None);
            first = await service.AddLineAsync(first.Id, "SALE-TEST", null, 2, 1m,
                CancellationToken.None);
            Assert.Equal(12m, first.Lines.Single().UnitPrice);
            Assert.Equal(6.5m, first.Lines.Single().UnitCostSnapshot);
            first = await service.ApplyLineDiscountAsync(first.Id, first.Lines.Single().Id,
                SaleDiscountType.Percentage, 25m, CancellationToken.None);
            first = await service.ApplySaleDiscountAsync(first.Id, SaleDiscountType.FixedAmount,
                1m, CancellationToken.None);
            Assert.Equal(24m, first.Subtotal);
            Assert.Equal(17m, first.Total);
            Assert.Null(first.SaleNumber);

            first = await service.CompleteAsync(first.Id, cashier, CancellationToken.None);
            Assert.Equal("Completed", first.Status);
            Assert.StartsWith("SALE-", first.SaleNumber);
            Assert.Equal("Percentage", first.Lines.Single().DiscountType);
            Assert.Equal(25m, first.Lines.Single().DiscountValue);
            Assert.Equal(6m, first.Lines.Single().DiscountAmount);
            Assert.Equal("FixedAmount", first.DiscountType);
            Assert.Equal(1m, first.DiscountAmount);
            await Assert.ThrowsAsync<ConflictException>(() => service.CompleteAsync(first.Id,
                cashier, CancellationToken.None));
            sales.ChangeTracker.Clear();
            inventory.ChangeTracker.Clear();
            Assert.Equal(8, (await inventory.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.Contains(await inventory.StockMovements.ToListAsync(), x =>
                x.MovementType == StockMovementType.Sale && x.ReferenceId == first.Id &&
                x.QuantityDelta == -2);

            var payments = new PaymentService(sales);
            var firstPaymentId = Guid.NewGuid();
            await payments.RecordAsync(firstPaymentId, first.Id, 10m, PaymentMethod.Cash,
                cashier, CancellationToken.None);
            await Assert.ThrowsAsync<ConflictException>(() => payments.RecordAsync(firstPaymentId,
                first.Id, 10m, PaymentMethod.Cash, cashier, CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() => payments.RecordAsync(Guid.NewGuid(),
                first.Id, 8m, PaymentMethod.Card, cashier, CancellationToken.None));
            Assert.Equal(7m, (await service.GetByIdAsync(first.Id, CancellationToken.None)).BalanceDue);

            await sales.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER sales.RejectPaymentTest ON sales.Payments
                AFTER INSERT AS BEGIN THROW 51003, 'Test payment write failure', 1; END
                """);
            var failedPaymentId = Guid.NewGuid();
            await Assert.ThrowsAnyAsync<Exception>(() => payments.RecordAsync(failedPaymentId,
                first.Id, 7m, PaymentMethod.Card, cashier, CancellationToken.None));
            await sales.Database.ExecuteSqlRawAsync("DROP TRIGGER sales.RejectPaymentTest");
            sales.ChangeTracker.Clear();
            Assert.Equal(7m, (await service.GetByIdAsync(first.Id, CancellationToken.None)).BalanceDue);
            Assert.False(await sales.Payments.AnyAsync(x => x.Id == failedPaymentId));

            await payments.RecordAsync(Guid.NewGuid(), first.Id, 7m, PaymentMethod.Card,
                cashier, CancellationToken.None);
            Assert.Equal(0m, (await service.GetByIdAsync(first.Id, CancellationToken.None)).BalanceDue);
            Assert.Equal(17m, (await service.GetByIdAsync(first.Id, CancellationToken.None)).PaidAmount);
            Assert.Equal(2, (await payments.ListBySaleAsync(first.Id, CancellationToken.None)).Count);

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

            sales.ChangeTracker.Clear();
            var returns = new SaleReturnService(sales, tenant);
            var saleLineId = first.Lines.Single().Id;
            var partialId = Guid.NewGuid();
            var partial = await returns.ProcessAsync(partialId, first.Id, location.Id,
                "Customer changed size", cashier,
                [new ReturnSaleLine(saleLineId, 1, true)], CancellationToken.None);
            Assert.Equal(8.5m, partial.RefundTotal);
            Assert.Equal(8.5m, partial.Lines.Single().UnitRefundAmount);
            Assert.Equal("PartiallyReturned", (await service.GetByIdAsync(first.Id,
                CancellationToken.None)).Status);
            Assert.Equal(1, (await service.GetByIdAsync(first.Id,
                CancellationToken.None)).Lines.Single().ReturnedQuantity);
            inventory.ChangeTracker.Clear();
            Assert.Equal(9, (await inventory.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.Contains(await inventory.StockMovements.ToListAsync(), x =>
                x.MovementType == StockMovementType.SaleReturn && x.ReferenceId == partialId &&
                x.QuantityDelta == 1);
            await Assert.ThrowsAsync<ConflictException>(() => returns.ProcessAsync(partialId,
                first.Id, location.Id, "Duplicate", cashier,
                [new ReturnSaleLine(saleLineId, 1, true)], CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() => returns.ProcessAsync(Guid.NewGuid(),
                first.Id, location.Id, "Too many", cashier,
                [new ReturnSaleLine(saleLineId, 2, true)], CancellationToken.None));

            await inventory.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER inventory.RejectReturnTest ON inventory.StockMovements
                AFTER INSERT AS BEGIN THROW 51004, 'Test return stock failure', 1; END
                """);
            var failedReturnId = Guid.NewGuid();
            await Assert.ThrowsAnyAsync<Exception>(() => returns.ProcessAsync(failedReturnId,
                first.Id, location.Id, "Rollback check", cashier,
                [new ReturnSaleLine(saleLineId, 1, true)], CancellationToken.None));
            await inventory.Database.ExecuteSqlRawAsync("DROP TRIGGER inventory.RejectReturnTest");
            sales.ChangeTracker.Clear();
            inventory.ChangeTracker.Clear();
            Assert.False(await sales.SaleReturns.AnyAsync(x => x.Id == failedReturnId));
            Assert.Equal("PartiallyReturned", (await service.GetByIdAsync(first.Id,
                CancellationToken.None)).Status);
            Assert.Equal(9, (await inventory.StockBalances.SingleAsync()).QuantityOnHand);

            var finalId = Guid.NewGuid();
            var finalReturn = await returns.ProcessAsync(finalId, first.Id, location.Id,
                "Damaged item", cashier,
                [new ReturnSaleLine(saleLineId, 1, false)], CancellationToken.None);
            Assert.Equal(8.5m, finalReturn.RefundTotal);
            Assert.Equal("Returned", (await service.GetByIdAsync(first.Id,
                CancellationToken.None)).Status);
            Assert.Equal(2, (await service.GetByIdAsync(first.Id,
                CancellationToken.None)).Lines.Single().ReturnedQuantity);
            inventory.ChangeTracker.Clear();
            Assert.Equal(9, (await inventory.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.DoesNotContain(await inventory.StockMovements.ToListAsync(), x =>
                x.MovementType == StockMovementType.SaleReturn && x.ReferenceId == finalId);
            Assert.Equal(2, (await returns.ListBySaleAsync(first.Id, CancellationToken.None)).Count);
            Assert.Equal(partial.Id, (await returns.GetByIdAsync(partialId,
                CancellationToken.None)).Id);
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
