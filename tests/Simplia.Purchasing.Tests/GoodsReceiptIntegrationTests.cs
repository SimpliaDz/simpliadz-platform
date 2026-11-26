using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Core.ValueObjects;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Purchasing.Core.Entities;
using Simplia.Purchasing.Infrastructure.Data;
using Simplia.Purchasing.Infrastructure.Services;
using Simplia.Purchasing.UseCases.Services;
using Xunit;

namespace Simplia.Purchasing.Tests;

public sealed class GoodsReceiptIntegrationTests
{
    [Fact]
    public async Task Partial_full_duplicate_and_failed_stock_write_are_atomic()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;

        var name = $"SimpliaReceiptTest_{Guid.NewGuid():N}";
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
                [new ReceiveGoodsLine(line.Id, 70)], "receiver", CancellationToken.None, "  Delivery #70  ");
            Assert.Equal("Delivery #70", (await service.GetByIdAsync(firstId, CancellationToken.None)).DeliveryReference);
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

            var paymentId = Guid.NewGuid();
            var payDate = new DateOnly(2026, 11, 25);
            complete.ValidateSupplierPayment(200m, 0m);
            purchasing.SupplierPayments.Add(new SupplierPayment(paymentId, complete.Id, 200m,
                SupplierPaymentMethod.Cash, payDate, Guid.NewGuid(), "Deposit", null));
            await purchasing.SaveChangesAsync();
            var paid = await purchasing.SupplierPayments.Where(x => x.PurchaseOrderId == complete.Id)
                .SumAsync(x => x.Amount);
            Assert.Equal(200m, paid);
            complete.ValidateSupplierPayment(complete.Total - paid, paid);
            purchasing.SupplierPayments.Add(new SupplierPayment(Guid.NewGuid(), complete.Id,
                complete.Total - paid, SupplierPaymentMethod.BankTransfer, payDate, Guid.NewGuid(), null, null));
            await purchasing.SaveChangesAsync();
            purchasing.ChangeTracker.Clear();
            var posted = await purchasing.SupplierPayments.AsNoTracking()
                .Where(x => x.PurchaseOrderId == complete.Id).ToListAsync();
            Assert.Equal(complete.Total, posted.Sum(x => x.Amount));
            Assert.Equal("Deposit", posted.Single(x => x.Id == paymentId).Reference);

            purchasing.ChangeTracker.Clear();
            var cancelled = await orders.CreateDraftAsync(supplier.Id, location.Id,
                "Original note", "buyer", CancellationToken.None);
            cancelled = await orders.UpdateNotesAsync(cancelled.Id, "Revised note",
                CancellationToken.None);
            Assert.Equal("Revised note", (await orders.GetByIdAsync(cancelled.Id,
                CancellationToken.None)).Notes);
            Assert.Contains(await orders.ListAsync(supplier.Id, "Draft", 1, 20,
                CancellationToken.None), x => x.Id == cancelled.Id);
            await orders.CancelAsync(cancelled.Id, CancellationToken.None);
            Assert.Equal("Cancelled", (await orders.GetByIdAsync(cancelled.Id,
                CancellationToken.None)).Status);
            await Assert.ThrowsAsync<ConflictException>(() => orders.PlaceOrderAsync(
                cancelled.Id, CancellationToken.None));
            Assert.Equal(2, await inventory.StockMovements.CountAsync());
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
