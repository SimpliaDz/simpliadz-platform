using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.Core.ValueObjects;
using Simplia.Inventory.Infrastructure;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Inventory.UseCases.Products.Abstractions;
using Simplia.Inventory.UseCases.Variants;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class GeneratedBarcodeIntegrationTests
{
    [Fact]
    public async Task Generated_barcode_is_persisted_and_searchable_without_replacing_factory_codes()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaBarcodeTest_{Guid.NewGuid():N}";
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
                Assert.Single(await db.BarcodeLabelTemplates.ToListAsync());
                Assert.Equal(3, await db.InventoryDocumentTemplates.CountAsync());
                var product = new Product("Barcode test product");
                db.Products.Add(product);
                db.ProductVariants.Add(new ProductVariant(product.Id, new Sku("BC-ONE"), 0));
                db.ProductVariants.Add(new ProductVariant(product.Id, new Sku("BC-FACTORY"), 0,
                    barcode: "FACTORY-BC"));
                var collisionTarget = new ProductVariant(product.Id, new Sku("BC-COLLISION"), 0);
                db.ProductVariants.Add(collisionTarget);
                db.ProductVariants.Add(new ProductVariant(product.Id, new Sku("BC-RESERVED"), 0,
                    barcode: "2000000000008"));
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
            Assert.True(InternalBarcode.IsValidEan13(result.Barcode));
            Assert.Equal(target.Id, (await products.GetVariantByBarcodeAsync(result.Barcode!,
                CancellationToken.None))!.Id);
            await using (var verify = NewDb(connection))
                Assert.Equal(result.Barcode, (await verify.ProductVariants.SingleAsync(x => x.Id == target.Id)).Barcode);
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
                new GenerateVariantBarcodeCommand(target.Id), CancellationToken.None).AsTask());
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
                new GenerateVariantBarcodeCommand(factory.Id), CancellationToken.None).AsTask());
            await Assert.ThrowsAsync<ConflictException>(() => products.TryAssignGeneratedBarcodeAsync(
                collision.Id, "2000000000008", CancellationToken.None));
            var collisionResult = await handler.Handle(new GenerateVariantBarcodeCommand(collision.Id),
                CancellationToken.None);
            Assert.True(InternalBarcode.IsValidEan13(collisionResult.Barcode));
            Assert.NotEqual("2000000000008", collisionResult.Barcode);
            var update = new UpdateVariantIdentifiersHandler(products);
            var corrected = await update.Handle(new UpdateVariantIdentifiersCommand(target.Id,
                "BC-RENAMED", "CORRECTED-BC"), CancellationToken.None);
            Assert.Equal("BC-RENAMED", corrected.Sku);
            Assert.Equal("CORRECTED-BC", corrected.Barcode);
            await using (var verify = NewDb(connection))
                Assert.Equal("CORRECTED-BC", (await verify.ProductVariants.SingleAsync(x =>
                    x.Id == target.Id)).Barcode);
            await Assert.ThrowsAsync<ConflictException>(() => update.Handle(
                new UpdateVariantIdentifiersCommand(target.Id, "BC-FACTORY", null),
                CancellationToken.None).AsTask());
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
