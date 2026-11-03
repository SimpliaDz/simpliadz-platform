using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.ValueObjects;
using SimpliaDz.Inventory.Infrastructure;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.UseCases.Services;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class InventoryPolicyIntegrationTests
{
    [Fact]
    public async Task Legacy_thresholds_are_preserved_and_policies_report_low_and_zero_stock()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzPolicyTest_{Guid.NewGuid():N}";
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
            using var provider = NewServices(connection);
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await db.GetService<IMigrator>().MigrateAsync(
                "20261102232818_Inventory_PosProductNameIndex");
            var product = new Product("Policy test product");
            var firstVariant = new ProductVariant(product.Id, new Sku("POLICY-ONE"), 0);
            var secondVariant = new ProductVariant(product.Id, new Sku("POLICY-TWO"), 0);
            var firstLocation = new InventoryLocation("Policy store");
            var secondLocation = new InventoryLocation("Policy warehouse");
            db.Products.Add(product);
            db.ProductVariants.AddRange(firstVariant, secondVariant);
            db.Locations.AddRange(firstLocation, secondLocation);
            await db.SaveChangesAsync();
            var balance = new StockBalance(firstVariant.Id, firstLocation.Id);
            balance.ApplyDelta(3);
            db.StockBalances.Add(balance);
            db.StockMovements.Add(new StockMovement(firstVariant.Id, firstLocation.Id,
                StockMovementType.OpeningBalance, 3, "tester"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [inventory].[StockItems] SET [ReorderPoint] = 6
                WHERE [VariantId] = {firstVariant.Id} AND [LocationId] = {firstLocation.Id}
                """);
            db.ChangeTracker.Clear();
            await db.Database.MigrateAsync();

            var policies = scope.ServiceProvider.GetRequiredService<IInventoryPolicyService>();
            var migrated = await policies.GetAsync(firstVariant.Id, firstLocation.Id,
                CancellationToken.None);
            Assert.Equal(6, migrated.ReorderPoint);
            Assert.Equal(3, migrated.QuantityOnHand);
            Assert.True(migrated.IsBelowReorderPoint);
            Assert.False(migrated.IsOutOfStock);

            var empty = await policies.ConfigureAsync(secondVariant.Id, firstLocation.Id,
                2, null, CancellationToken.None);
            Assert.Equal(0, empty.QuantityOnHand);
            Assert.True(empty.IsBelowReorderPoint);
            Assert.True(empty.IsOutOfStock);
            var zeroThreshold = await policies.ConfigureAsync(firstVariant.Id,
                secondLocation.Id, 0, null, CancellationToken.None);
            Assert.True(zeroThreshold.IsOutOfStock);
            Assert.False(zeroThreshold.IsBelowReorderPoint);
            Assert.Equal(2, (await policies.ListAsync(null, true, null, 1, 50,
                CancellationToken.None)).Count);
            Assert.Equal(2, (await policies.ListAsync(null, null, true, 1, 50,
                CancellationToken.None)).Count);
            Assert.Equal(2, (await policies.ListAsync(firstLocation.Id, null, null, 1, 50,
                CancellationToken.None)).Count);
            var outAtStore = await policies.GetOutOfStockByLocationAsync(firstLocation.Id,
                1, 50, CancellationToken.None);
            Assert.Single(outAtStore);
            Assert.Equal(secondVariant.Id, outAtStore.Single().ProductVariantId);
            var outAtWarehouse = await policies.GetOutOfStockByLocationAsync(secondLocation.Id,
                1, 50, CancellationToken.None);
            Assert.Equal(2, outAtWarehouse.Count);
            Assert.Contains(outAtWarehouse, x => x.ProductVariantId == secondVariant.Id &&
                x.ReorderPoint is null);

            var updated = await policies.ConfigureAsync(firstVariant.Id,
                firstLocation.Id, 4, 10, CancellationToken.None);
            Assert.Equal(4, updated.ReorderPoint);
            Assert.Equal(10, updated.TargetStockLevel);
            Assert.Equal(3, await db.InventoryPolicies.CountAsync());
            Assert.Single(await db.StockBalances.ToListAsync());
            Assert.Single(await db.StockMovements.ToListAsync());
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

    private static ServiceProvider NewServices(string connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TestTenant(connection));
        new InventoryModuleInstaller().Register(services, connection);
        return services.BuildServiceProvider();
    }

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName == "Inventory";
    }
}
