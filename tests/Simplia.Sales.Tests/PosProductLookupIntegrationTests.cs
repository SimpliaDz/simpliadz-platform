using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Core.ValueObjects;
using Simplia.Inventory.Infrastructure.Data;
using Xunit;

namespace Simplia.Sales.Tests;

public sealed class PosProductLookupIntegrationTests
{
    [Fact]
    public async Task Lookup_uses_exact_codes_and_returns_active_priced_variants_with_location_stock()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaPosLookupTest_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(baseConnection)
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
            await using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
                .UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory")).Options);
            await db.Database.MigrateAsync();
            var product = new Product("Classic Cotton T-Shirt");
            var variant = new ProductVariant(product.Id, new Sku("TEE-BLK-M"), 0,
                barcode: "6123456789012", color: "Black", size: "M");
            var hiddenVariant = new ProductVariant(product.Id, new Sku("TEE-HIDDEN"), 0,
                barcode: "HIDDEN-BARCODE");
            hiddenVariant.Deactivate();
            var hiddenProduct = new Product("Classic Hidden Product");
            hiddenProduct.Deactivate();
            var hiddenProductVariant = new ProductVariant(hiddenProduct.Id, new Sku("HIDDEN-PRODUCT"), 0,
                barcode: "HIDDEN-PRODUCT-BARCODE");
            var location = new InventoryLocation("POS Store");
            var priceList = new PriceList("POS Retail", "DZD", PriceListType.Retail);
            priceList.SetPrice(variant.Id, 1500m);
            priceList.SetPrice(hiddenVariant.Id, 1500m);
            priceList.SetPrice(hiddenProductVariant.Id, 1500m);
            db.Products.AddRange(product, hiddenProduct);
            db.ProductVariants.AddRange(variant, hiddenVariant, hiddenProductVariant);
            db.Locations.Add(location);
            db.PriceLists.Add(priceList);
            await db.SaveChangesAsync();

            var balance = new StockBalance(variant.Id, location.Id);
            balance.ApplyDelta(7);
            db.StockBalances.Add(balance);
            db.StockMovements.Add(new StockMovement(variant.Id, location.Id,
                StockMovementType.OpeningBalance, 7, "user"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var lookup = new PosProductLookupService(db, new TestTenant(connectionString));
            var byBarcode = await lookup.FindByBarcodeAsync("6123456789012", priceList.Id,
                location.Id, CancellationToken.None);
            Assert.NotNull(byBarcode);
            Assert.Equal(variant.Id, byBarcode.VariantId);
            Assert.Equal("Classic Cotton T-Shirt", byBarcode.ProductName);
            Assert.Equal("Black", byBarcode.Color);
            Assert.Equal(1500m, byBarcode.Price);
            Assert.Equal("DZD", byBarcode.CurrencyCode);
            Assert.Equal(7, byBarcode.QuantityOnHand);
            Assert.Null(await lookup.FindByBarcodeAsync("123456789012", priceList.Id,
                location.Id, CancellationToken.None));
            Assert.Equal(variant.Id, (await lookup.FindBySkuAsync("TEE-BLK-M", priceList.Id,
                null, CancellationToken.None))!.VariantId);
            Assert.Null(await lookup.FindBySkuAsync("TEE-HIDDEN", priceList.Id,
                location.Id, CancellationToken.None));
            Assert.Null(await lookup.FindBySkuAsync("HIDDEN-PRODUCT", priceList.Id,
                location.Id, CancellationToken.None));

            var matches = await lookup.SearchByProductNameAsync("Classic Cotton", priceList.Id,
                location.Id, 1, 25, CancellationToken.None);
            Assert.Single(matches);
            Assert.Equal(variant.Id, matches[0].VariantId);
            Assert.Empty(await lookup.SearchByProductNameAsync("Cotton", priceList.Id,
                location.Id, 1, 25, CancellationToken.None));
        }
        finally
        {
            await using var cleanup = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
                .UseSqlServer(connectionString).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestTenant(string connectionString) : ITenantContext
    {
        public string TenantId => "integration";
        public string Name => "integration";
        public string ConnectionString => connectionString;
        public bool IsModuleEnabled(string moduleName) => moduleName is "Inventory" or "Sales";
    }
}
