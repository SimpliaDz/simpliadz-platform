using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Infrastructure;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Attributes;
using SimpliaDz.Inventory.UseCases.Attributes.Abstractions;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;
using SimpliaDz.Inventory.UseCases.Variants;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class AttributePersistenceIntegrationTests
{
    [Fact]
    public async Task New_value_is_inserted_for_existing_attribute_and_can_be_reordered()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzAttributeTest_{Guid.NewGuid():N}";
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
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContext>(new TestTenant(connection));
            new InventoryModuleInstaller().Register(services, connection);
            await using var provider = services.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                await db.Database.MigrateAsync();
                var repository = scope.ServiceProvider.GetRequiredService<IAttributeRepository>();
                var attribute = await new CreateAttributeHandler(repository).Handle(
                    new CreateAttributeCommand("Material"), CancellationToken.None);
                var value = await new CreateAttributeValueHandler(repository).Handle(
                    new CreateAttributeValueCommand(attribute.Id, "Cotton", 1), CancellationToken.None);
                await new SetAttributeValueOrderHandler(repository).Handle(
                    new SetAttributeValueOrderCommand(attribute.Id, value.Id, 2), CancellationToken.None);
                var product = new Product("Test shirt");
                db.Products.Add(product);
                await db.SaveChangesAsync();
                var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                var variant = await new CreateVariantHandler(products).Handle(
                    new CreateVariantCommand(product.Id, "TEST-SHIRT-M", null,
                        1000m, null, [value.Id]), CancellationToken.None);
                Assert.Equal(product.Id, variant.ProductId);
            }

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var saved = await db.AttributeValues.SingleAsync(x => x.Value == "Cotton");
                Assert.Equal(2, saved.DisplayOrder);
                var attributeId = await db.ProductAttributes
                    .Where(x => x.Name == "Material").Select(x => x.Id).SingleAsync();
                Assert.Equal(attributeId, saved.AttributeId);
                Assert.Equal(1, await db.ProductVariants.CountAsync());
            }
        }
        finally
        {
            await using var cleanup = new InventoryDbContext(
                new DbContextOptionsBuilder<InventoryDbContext>()
                    .UseSqlServer(connection, sql =>
                        sql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
                    .Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "attribute-test";
        public string Name => "attribute-test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName == "Inventory";
    }
}
