using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Core.Exceptions;
using Simplia.Inventory.Core.ValueObjects;
using Simplia.Inventory.Infrastructure;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Inventory.UseCases.Services;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class StockOperationsIntegrationTests
{
    [Fact]
    public async Task Opening_receiving_and_adjustments_preserve_a_movement_for_every_change()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            var (variant, source, _) = await SeedAsync(db);

            var opening = await stock.OpenAsync(variant.Id, source.Id, 10, "Initial count",
                "manager", CancellationToken.None);
            Assert.Equal(10, opening.QuantityOnHand);
            await Assert.ThrowsAsync<ConflictException>(() =>
                stock.OpenAsync(variant.Id, source.Id, 2, null, "manager", CancellationToken.None));
            await stock.ReceiveAsync(variant.Id, source.Id, 5, "PO-1", null, "GoodsReceipt",
                Guid.NewGuid(), "receiver", CancellationToken.None);
            await stock.AdjustByAsync(variant.Id, source.Id, -3, "Damage", "manager",
                CancellationToken.None);
            await stock.AdjustToAsync(variant.Id, source.Id, 11, "Count correction", "manager",
                CancellationToken.None);
            await stock.AdjustToAsync(variant.Id, source.Id, 11, "No change", "manager",
                CancellationToken.None);

            Assert.Equal(11, await stock.GetTotalByVariantAsync(variant.Id, CancellationToken.None));
            Assert.Equal(11, Assert.Single(await stock.GetByLocationAsync(source.Id,
                CancellationToken.None)).QuantityOnHand);
            var movements = await stock.GetMovementsAsync(variant.Id, source.Id, 1, 50,
                CancellationToken.None);
            Assert.Equal(4, movements.Count);
            Assert.Equal(11, movements.Sum(x => x.QuantityDelta));
            Assert.Contains(movements, x => x.MovementType == "OpeningBalance" &&
                x.QuantityDelta == 10 && x.CreatedBy == "manager");
            Assert.Contains(movements, x => x.MovementType == "PurchaseReceipt" &&
                x.QuantityDelta == 5 && x.CreatedBy == "receiver");
            Assert.Equal(11, (await db.StockBalances.SingleAsync()).QuantityOnHand);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                stock.AdjustByAsync(variant.Id, source.Id, 0, "Invalid", "manager",
                    CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                stock.AdjustByAsync(variant.Id, source.Id, 1, "", "manager",
                    CancellationToken.None));
            await Assert.ThrowsAsync<InsufficientStockException>(() =>
                stock.AdjustByAsync(variant.Id, source.Id, -12, "Too much", "manager",
                    CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Equal(11, await stock.GetTotalByVariantAsync(variant.Id, CancellationToken.None));
            Assert.Equal(4, await db.StockMovements.CountAsync());
        });
    }

    [Fact]
    public async Task Completing_a_multi_line_transfer_moves_stock_and_creates_paired_movements_once()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            var transfers = scope.ServiceProvider.GetRequiredService<IStockTransferService>();
            var (first, source, destination) = await SeedAsync(db);
            var second = new ProductVariant((await db.Products.SingleAsync()).Id,
                new Sku("TRANSFER-SECOND"), 0);
            db.ProductVariants.Add(second);
            await db.SaveChangesAsync();
            await stock.OpenAsync(first.Id, source.Id, 10, null, "manager", CancellationToken.None);
            await stock.OpenAsync(second.Id, source.Id, 3, null, "manager", CancellationToken.None);

            var draft = await transfers.CreateDraftAsync(source.Id, destination.Id, "manager",
                CancellationToken.None);
            db.ChangeTracker.Clear();
            await transfers.AddLineAsync(draft.Id, first.Id, 4, CancellationToken.None);
            db.ChangeTracker.Clear();
            await transfers.AddLineAsync(draft.Id, second.Id, 2, CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Equal(0, await db.StockMovements.CountAsync(x =>
                x.ReferenceId == draft.Id));
            Assert.Equal(2, (await transfers.GetByIdAsync(draft.Id, CancellationToken.None)).Lines.Count);
            Assert.Contains(await transfers.ListAsync(1, 10, CancellationToken.None),
                transfer => transfer.Id == draft.Id);

            var completed = await transfers.CompleteAsync(draft.Id, "supervisor",
                CancellationToken.None);
            Assert.Equal("Completed", completed.Status);
            db.ChangeTracker.Clear();
            Assert.Equal(10, await stock.GetTotalByVariantAsync(first.Id, CancellationToken.None));
            Assert.Equal(3, await stock.GetTotalByVariantAsync(second.Id, CancellationToken.None));
            var rows = await db.StockBalances.AsNoTracking().ToListAsync();
            Assert.Equal(6, rows.Single(x => x.VariantId == first.Id &&
                x.LocationId == source.Id).QuantityOnHand);
            Assert.Equal(4, rows.Single(x => x.VariantId == first.Id &&
                x.LocationId == destination.Id).QuantityOnHand);
            Assert.Equal(1, rows.Single(x => x.VariantId == second.Id &&
                x.LocationId == source.Id).QuantityOnHand);
            Assert.Equal(2, rows.Single(x => x.VariantId == second.Id &&
                x.LocationId == destination.Id).QuantityOnHand);
            var movements = await db.StockMovements.AsNoTracking()
                .Where(x => x.ReferenceId == draft.Id).ToListAsync();
            Assert.Equal(4, movements.Count);
            Assert.Equal(0, movements.Sum(x => x.QuantityDelta));
            Assert.Equal(2, movements.Count(x => x.MovementType == StockMovementType.TransferOut));
            Assert.Equal(2, movements.Count(x => x.MovementType == StockMovementType.TransferIn));
            Assert.All(movements, x => Assert.Equal("supervisor", x.CreatedBy));
            await Assert.ThrowsAsync<ConflictException>(() =>
                transfers.CompleteAsync(draft.Id, "supervisor", CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() =>
                transfers.AddLineAsync(draft.Id, first.Id, 1, CancellationToken.None));
        });
    }

    [Fact]
    public async Task Insufficient_and_cancelled_transfers_leave_balances_and_ledger_unchanged()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            var transfers = scope.ServiceProvider.GetRequiredService<IStockTransferService>();
            var (variant, source, destination) = await SeedAsync(db);
            await stock.OpenAsync(variant.Id, source.Id, 2, null, "manager",
                CancellationToken.None);
            var draft = await transfers.CreateDraftAsync(source.Id, destination.Id, "manager",
                CancellationToken.None);
            db.ChangeTracker.Clear();
            await transfers.AddLineAsync(draft.Id, variant.Id, 3, CancellationToken.None);
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<InsufficientStockException>(() =>
                transfers.CompleteAsync(draft.Id, "manager", CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Equal("Draft", (await transfers.GetByIdAsync(draft.Id,
                CancellationToken.None)).Status);
            Assert.Equal(2, await stock.GetTotalByVariantAsync(variant.Id, CancellationToken.None));
            Assert.Equal(0, await db.StockMovements.CountAsync(x => x.ReferenceId == draft.Id));

            await transfers.CancelAsync(draft.Id, CancellationToken.None);
            Assert.Equal("Cancelled", (await transfers.GetByIdAsync(draft.Id,
                CancellationToken.None)).Status);
            await Assert.ThrowsAsync<ConflictException>(() =>
                transfers.CompleteAsync(draft.Id, "manager", CancellationToken.None));
            Assert.Equal(2, await stock.GetTotalByVariantAsync(variant.Id, CancellationToken.None));
        });
    }

    [Fact]
    public async Task Failed_transfer_movement_write_rolls_back_both_balances_and_status()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            var transfers = scope.ServiceProvider.GetRequiredService<IStockTransferService>();
            var (variant, source, destination) = await SeedAsync(db);
            await stock.OpenAsync(variant.Id, source.Id, 5, null, "manager",
                CancellationToken.None);
            var draft = await transfers.CreateDraftAsync(source.Id, destination.Id, "manager",
                CancellationToken.None);
            db.ChangeTracker.Clear();
            await transfers.AddLineAsync(draft.Id, variant.Id, 2, CancellationToken.None);
            db.ChangeTracker.Clear();

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER inventory.RejectTransferTest ON inventory.StockMovements
                AFTER INSERT AS BEGIN
                    IF EXISTS (SELECT 1 FROM inserted WHERE [Type] = 6)
                        THROW 51001, 'Test transfer failure', 1;
                END
                """);
            try
            {
                await Assert.ThrowsAnyAsync<Exception>(() =>
                    transfers.CompleteAsync(draft.Id, "manager", CancellationToken.None));
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER inventory.RejectTransferTest");
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Draft", (await transfers.GetByIdAsync(draft.Id,
                CancellationToken.None)).Status);
            Assert.Equal(5, await stock.GetTotalByVariantAsync(variant.Id,
                CancellationToken.None));
            Assert.False(await db.StockBalances.AnyAsync(x =>
                x.VariantId == variant.Id && x.LocationId == destination.Id));
            Assert.Equal(0, await db.StockMovements.CountAsync(x => x.ReferenceId == draft.Id));
        });
    }

    private static async Task<(ProductVariant Variant, InventoryLocation Source,
        InventoryLocation Destination)> SeedAsync(InventoryDbContext db)
    {
        var product = new Product("Transfer test product");
        var variant = new ProductVariant(product.Id, new Sku("TRANSFER-FIRST"), 0);
        var source = new InventoryLocation("Source");
        var destination = new InventoryLocation("Destination");
        db.Products.Add(product);
        db.ProductVariants.Add(variant);
        db.Locations.AddRange(source, destination);
        await db.SaveChangesAsync();
        return (variant, source, destination);
    }

    private static async Task InDatabaseAsync(Func<IServiceProvider, Task> test)
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaStockTest_{Guid.NewGuid():N}";
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
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContext>(new TestTenant(connection));
            var installer = new InventoryModuleInstaller();
            installer.Register(services, connection);
            await using var provider = services.BuildServiceProvider();
            await installer.MigrateAsync(provider, CancellationToken.None);
            await test(provider);
        }
        finally
        {
            await using var cleanup = new InventoryDbContext(
                new DbContextOptionsBuilder<InventoryDbContext>()
                    .UseSqlServer(connection).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "stock-test";
        public string Name => "stock-test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName == "Inventory";
    }
}
