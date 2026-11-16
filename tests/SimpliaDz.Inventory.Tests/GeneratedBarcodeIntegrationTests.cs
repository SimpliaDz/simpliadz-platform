using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.ValueObjects;
using SimpliaDz.Inventory.Infrastructure;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;
using SimpliaDz.Inventory.UseCases.Variants;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class GeneratedBarcodeIntegrationTests
{
    [Fact]
    public async Task Generated_barcode_is_persisted_and_searchable_without_replacing_factory_codes()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzBarcodeTest_{Guid.NewGuid():N}";
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
            await using (var db = NewDb(connection))
            {
                await db.Database.MigrateAsync();
                var product = new Product("Barcode test product");
                db.Products.Add(product);
                db.ProductVariants.Add(new ProductVariant(product.Id, new Sku("BC-ONE"), 0));
                db.ProductVariants.Add(new ProductVariant(product.Id, new Sku("BC-FACTORY"), 0,
                    barcode: "FACTORY-BC"));
                var collisionTarget = new ProductVariant(product.Id, new Sku("BC-COLLISION"), 0);
                db.ProductVariants.Add(collisionTarget);
                db.ProductVariants.Add(new ProductVariant(product.Id, new Sku("BC-RESERVED"), 0,
                    barcode: $"SDZ{collisionTarget.Id:N}".ToUpperInvariant()));
                await db.SaveChangesAsync();
            }

            var services = new ServiceCollection();
            services.AddSingleton<ITenantContext>(new TestTenant(connection));
            new InventoryModuleInstaller().Register(services, connection);
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var handler = new GenerateVariantBarcodeHandler(products);
            var target = await products.GetVariantBySkuAsync("BC-ONE", CancellationToken.None);
            var factory = await products.GetVariantBySkuAsync("BC-FACTORY", CancellationToken.None);
            var collision = await products.GetVariantBySkuAsync("BC-COLLISION", CancellationToken.None);
            Assert.NotNull(target);
            Assert.NotNull(factory);
            Assert.NotNull(collision);

            var result = await handler.Handle(new GenerateVariantBarcodeCommand(target.Id),
                CancellationToken.None);
            Assert.Equal(target.Id, (await products.GetVariantByBarcodeAsync(result.Barcode!,
                CancellationToken.None))!.Id);
            await using (var verify = NewDb(connection))
                Assert.Equal(result.Barcode, (await verify.ProductVariants.SingleAsync(x => x.Id == target.Id)).Barcode);
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
                new GenerateVariantBarcodeCommand(target.Id), CancellationToken.None).AsTask());
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
                new GenerateVariantBarcodeCommand(factory.Id), CancellationToken.None).AsTask());
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
                new GenerateVariantBarcodeCommand(collision.Id), CancellationToken.None).AsTask());
            await using (var verify = NewDb(connection))
                Assert.Equal("FACTORY-BC", (await verify.ProductVariants.SingleAsync(x => x.Id == factory.Id)).Barcode);
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

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "barcode-test";
        public string Name => "barcode-test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName == "Inventory";
    }
}
