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
using SimpliaDz.Purchasing.Infrastructure.Services;
using SimpliaDz.Purchasing.UseCases.Services;
using SimpliaDz.Purchasing.UseCases.Services;
using Xunit;

namespace SimpliaDz.Purchasing.Tests;

public sealed class GoodsReceiptIntegrationTests
{
    [Fact]
    public async Task Partial_full_duplicate_and_failed_stock_write_are_atomic()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;

        var name = $"SimpliaDzReceiptTest_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = name };
        var connectionString = builder.ConnectionString;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(baseConnection)
                     { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var create = new SqlCommand($"CREATE DATABASE [{name}]", master);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            await using var inventory = NewInventory(connectionString);
            await using var purchasing = NewPurchasing(connectionString);
            await inventory.Database.MigrateAsync();
            await purchasing.Database.MigrateAsync();

            var product = new Product("Receipt test product");
            var variant = new ProductVariant(product.Id, new Sku("RECEIPT-TEST"), 0);
            var location = new InventoryLocation("Receipt test location");
            inventory.Products.Add(product);
            inventory.ProductVariants.Add(variant);
            inventory.Locations.Add(location);
            await inventory.SaveChangesAsync();

            var supplier = new Supplier("Receipt test supplier");
            purchasing.Suppliers.Add(supplier);
            await purchasing.SaveChangesAsync();
            purchasing.ChangeTracker.Clear();
            var orders = new PurchaseOrderService(purchasing, new ActiveInventoryReferences());
            var order = await orders.CreateDraftAsync(supplier.Id, location.Id, null,
                "buyer", CancellationToken.None);
            order = await orders.AddLineAsync(order.Id, variant.Id, 100, 12.5m,
                CancellationToken.None);
            var line = order.Lines.Single();
            order = await orders.PlaceOrderAsync(order.Id, CancellationToken.None);
            purchasing.ChangeTracker.Clear();

            var service = new GoodsReceiptService(purchasing, new TestTenant(connectionString));
            var firstId = Guid.NewGuid();
            var first = await service.ReceiveAsync(firstId, order.Id,
                [new ReceiveGoodsLine(line.Id, 70)], "receiver", CancellationToken.None);
            Assert.Equal(70, first.Lines.Single().QuantityReceived);
            await Assert.ThrowsAsync<ConflictException>(() => service.ReceiveAsync(firstId, order.Id,
                [new ReceiveGoodsLine(line.Id, 70)], "receiver", CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() => service.ReceiveAsync(Guid.NewGuid(), order.Id,
                [new ReceiveGoodsLine(line.Id, 31)], "receiver", CancellationToken.None));

            await inventory.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER inventory.RejectReceiptTest ON inventory.StockMovements
                AFTER INSERT AS BEGIN THROW 51000, 'Test stock write failure', 1; END
                """);
            var secondId = Guid.NewGuid();
            await Assert.ThrowsAnyAsync<Exception>(() => service.ReceiveAsync(secondId, order.Id,
                [new ReceiveGoodsLine(line.Id, 30)], "receiver", CancellationToken.None));
            await inventory.Database.ExecuteSqlRawAsync("DROP TRIGGER inventory.RejectReceiptTest");

            purchasing.ChangeTracker.Clear();
            inventory.ChangeTracker.Clear();
            Assert.False(await purchasing.GoodsReceipts.AnyAsync(x => x.Id == secondId));
            var partial = await purchasing.PurchaseOrders.Include(x => x.Lines)
                .SingleAsync(x => x.Id == order.Id);
            Assert.Equal(PurchaseOrderStatus.PartiallyReceived, partial.Status);
            Assert.Equal(70, partial.Lines.Single().ReceivedQuantity);
            Assert.Equal(70, (await inventory.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.Single(await inventory.StockMovements.ToListAsync());

            await service.ReceiveAsync(secondId, order.Id,
                [new ReceiveGoodsLine(line.Id, 30)], "receiver", CancellationToken.None);
            purchasing.ChangeTracker.Clear();
            inventory.ChangeTracker.Clear();
            var complete = await purchasing.PurchaseOrders.Include(x => x.Lines)
                .SingleAsync(x => x.Id == order.Id);
            Assert.Equal(PurchaseOrderStatus.Received, complete.Status);
            Assert.Equal(100, complete.Lines.Single().ReceivedQuantity);
            Assert.Equal(100, (await inventory.StockBalances.SingleAsync()).QuantityOnHand);
            var movements = await inventory.StockMovements.OrderBy(x => x.SourceLineNumber).ToListAsync();
            Assert.Equal(2, movements.Count);
            Assert.All(movements, x => Assert.Equal(StockMovementType.PurchaseReceipt, x.MovementType));
            Assert.Contains(movements, x => x.ReferenceId == firstId && x.QuantityDelta == 70);
            Assert.Contains(movements, x => x.ReferenceId == secondId && x.QuantityDelta == 30);
        }
        finally
        {
            await using var cleanup = NewPurchasing(connectionString);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static InventoryDbContext NewInventory(string connectionString) => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options);

    private static PurchasingDbContext NewPurchasing(string connectionString) => new(
        new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "purchasing"))
            .Options);

    private sealed class TestTenant(string connectionString) : ITenantContext
    {
        public string TenantId => "integration";
        public string Name => "integration";
        public string ConnectionString => connectionString;
        public bool IsModuleEnabled(string moduleName) =>
            moduleName is "Inventory" or "Purchasing";
    }

    private sealed class ActiveInventoryReferences : IInventoryReferenceValidator
    {
        public Task<bool> IsActiveLocationAsync(Guid locationId, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<bool> IsActiveVariantAsync(Guid variantId, CancellationToken ct) =>
            Task.FromResult(true);
    }
}
