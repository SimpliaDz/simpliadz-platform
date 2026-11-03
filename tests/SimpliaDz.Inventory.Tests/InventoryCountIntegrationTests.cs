using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.ValueObjects;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.Infrastructure.Services;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class InventoryCountIntegrationTests
{
    [Fact]
    public async Task Count_reconciles_both_directions_and_rolls_back_failed_completion()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;

        var name = $"SimpliaDzCountTest_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = name };
        var connectionString = builder.ConnectionString;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(baseConnection)
                     { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{name}]", master);
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await using var db = NewDb(connectionString);
            await db.Database.MigrateAsync();
            var product = new Product("Count test product");
            var variant = new ProductVariant(product.Id, new Sku("COUNT-TEST"), 0);
            var location = new InventoryLocation("Count test location");
            db.Products.Add(product);
            db.ProductVariants.Add(variant);
            db.Locations.Add(location);
            await db.SaveChangesAsync();

            var balance = new StockBalance(variant.Id, location.Id);
            balance.ApplyDelta(17);
            db.StockBalances.Add(balance);
            db.StockMovements.Add(new StockMovement(variant.Id, location.Id,
                StockMovementType.OpeningBalance, 17, "user"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var service = new InventoryCountService(db);
            var first = await service.StartAsync(location.Id, "user", CancellationToken.None);
            Assert.Equal(17, first.Lines.Single().ExpectedQuantity);
            first = await service.SetCountedQuantityAsync(first.Id, variant.Id, 15, CancellationToken.None);
            Assert.Equal(-2, first.Lines.Single().Difference);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER inventory.RejectCountTest ON inventory.StockMovements
                AFTER INSERT AS BEGIN THROW 51001, 'Test adjustment failure', 1; END
                """);
            await Assert.ThrowsAnyAsync<Exception>(() => service.CompleteAsync(first.Id,
                "manager", CancellationToken.None));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER inventory.RejectCountTest");
            db.ChangeTracker.Clear();
            Assert.Equal(InventoryCountStatus.Draft,
                (await db.InventoryCounts.SingleAsync(x => x.Id == first.Id)).Status);
            Assert.Equal(17, (await db.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.Single(await db.StockMovements.ToListAsync());

            first = await service.CompleteAsync(first.Id, "manager", CancellationToken.None);
            Assert.Equal("Completed", first.Status);
            await Assert.ThrowsAsync<ConflictException>(() => service.CompleteAsync(first.Id,
                "manager", CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Equal(15, (await db.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.Contains(await db.StockMovements.ToListAsync(), x =>
                x.ReferenceType == "InventoryCount" && x.ReferenceId == first.Id &&
                x.MovementType == StockMovementType.Adjustment && x.QuantityDelta == -2);

            var second = await service.StartAsync(location.Id, "user", CancellationToken.None);
            second = await service.SetCountedQuantityAsync(second.Id, variant.Id, 18,
                CancellationToken.None);
            await service.CompleteAsync(second.Id, "manager", CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Equal(18, (await db.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.Contains(await db.StockMovements.ToListAsync(), x =>
                x.ReferenceId == second.Id && x.QuantityDelta == 3);

            var stale = await service.StartAsync(location.Id, "user", CancellationToken.None);
            await service.SetCountedQuantityAsync(stale.Id, variant.Id, 16, CancellationToken.None);
            db.ChangeTracker.Clear();
            var changedBalance = await db.StockBalances.SingleAsync();
            changedBalance.ApplyDelta(1);
            db.StockMovements.Add(new StockMovement(variant.Id, location.Id,
                StockMovementType.PurchaseReceipt, 1, "other-user"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ConflictException>(() => service.CompleteAsync(stale.Id,
                "manager", CancellationToken.None));
            Assert.Equal(InventoryCountStatus.Draft,
                (await db.InventoryCounts.SingleAsync(x => x.Id == stale.Id)).Status);
            Assert.Equal(19, (await db.StockBalances.SingleAsync()).QuantityOnHand);
            Assert.Equal(3, (await service.ListAsync(location.Id, 1, 50,
                CancellationToken.None)).Count);
        }
        finally
        {
            await using var cleanup = NewDb(connectionString);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static InventoryDbContext NewDb(string connectionString) => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options);
}
