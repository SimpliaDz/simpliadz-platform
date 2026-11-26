using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.Api.Modules;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Core.ValueObjects;
using Simplia.Inventory.Infrastructure.Data;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class InventoryWarningIntegrationTests
{
    [Fact]
    public async Task Scheduled_scan_finds_low_and_aged_stock_without_duplicate_alerts()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaWarningTest_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnection)
            { InitialCatalog = name }.ConnectionString;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(baseConnection)
                     { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var create = new SqlCommand($"CREATE DATABASE [{name}]", master);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            await using var db = NewDb(connection);
            await db.Database.MigrateAsync();
            var product = new Product("Warning product");
            var variant = new ProductVariant(product.Id, new Sku("WARN-SKU"), 0);
            var location = new InventoryLocation("Warning store");
            var balance = new StockBalance(variant.Id, location.Id);
            balance.ApplyDelta(5);
            db.Products.Add(product);
            db.ProductVariants.Add(variant);
            db.Locations.Add(location);
            db.StockBalances.Add(balance);
            db.StockMovements.Add(new StockMovement(variant.Id, location.Id,
                StockMovementType.OpeningBalance, 5, "tester"));
            db.InventoryPolicies.Add(new InventoryPolicy(variant.Id, location.Id, 10, null));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.StockMovements SET OccurredAt = {DateTimeOffset.UtcNow.AddDays(-100)}
                WHERE VariantId = {variant.Id} AND LocationId = {location.Id}
                """);
            db.ChangeTracker.Clear();
            var settings = await db.InventoryWarningSettings.SingleAsync();
            settings.Configure(true, true, true, 60, 90);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var scan = new InventoryWarningScanService(db, TimeProvider.System);
            Assert.True(await scan.RunAsync(true, CancellationToken.None));
            Assert.Equal(2, await db.InventoryWarnings.CountAsync(x => x.IsActive));
            Assert.Contains(await db.InventoryWarnings.ToListAsync(), x =>
                x.Kind == InventoryWarningKind.LowStock && x.QuantityOnHand == 5 &&
                x.ReorderPoint == 10);
            Assert.Contains(await db.InventoryWarnings.ToListAsync(), x =>
                x.Kind == InventoryWarningKind.Ageing && x.StockEnteredAt.HasValue);
            Assert.False(await scan.RunAsync(false, CancellationToken.None));
            Assert.True(await scan.RunAsync(true, CancellationToken.None));
            Assert.Equal(2, await db.InventoryWarnings.CountAsync());

            db.ChangeTracker.Clear();
            var emptyBalance = await db.StockBalances.SingleAsync();
            emptyBalance.ApplyDelta(-5);
            db.StockMovements.Add(new StockMovement(variant.Id, location.Id,
                StockMovementType.Adjustment, -5, "tester"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var refilledBalance = await db.StockBalances.SingleAsync();
            refilledBalance.ApplyDelta(5);
            db.StockMovements.Add(new StockMovement(variant.Id, location.Id,
                StockMovementType.Adjustment, 5, "tester"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.True(await scan.RunAsync(true, CancellationToken.None));
            Assert.False((await db.InventoryWarnings.SingleAsync(x =>
                x.Kind == InventoryWarningKind.Ageing)).IsActive);
            Assert.True((await db.InventoryWarnings.SingleAsync(x =>
                x.Kind == InventoryWarningKind.LowStock)).IsActive);
        }
        finally
        {
            await using var cleanup = NewDb(connection);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static InventoryDbContext NewDb(string connection) => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options);
}
