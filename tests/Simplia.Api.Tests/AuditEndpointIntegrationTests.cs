using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Simplia.Inventory.Infrastructure.Data;
using Xunit;

namespace Simplia.Api.Tests;

public sealed class AuditEndpointIntegrationTests
{
    [Fact]
    public async Task Login_and_stock_adjustment_create_single_queryable_entries_without_credentials()
    {
        await ApiTestHost.RunAsync(async (client, services) =>
        {
            await ApiTestHost.LoginAsAdminAsync(client);

            var logins = await client.GetFromJsonAsync<JsonElement>(
                "/audit/entries?action=UserLoggedIn");
            Assert.Equal(1, logins.GetProperty("totalItems").GetInt64());
            var login = Assert.Single(logins.GetProperty("items").EnumerateArray());
            var actor = login.GetProperty("userId").GetGuid();
            Assert.Equal(actor, login.GetProperty("entityId").GetGuid());
            Assert.False(string.IsNullOrWhiteSpace(login.GetProperty("correlationId").GetString()));
            Assert.DoesNotContain("Admin123!", login.ToString());

            Guid variantId;
            Guid locationId;
            int before;
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                variantId = await db.ProductVariants.Select(x => x.Id).FirstAsync();
                locationId = await db.Locations.Select(x => x.Id).FirstAsync();
                before = await db.StockBalances
                    .Where(x => x.VariantId == variantId && x.LocationId == locationId)
                    .Select(x => (int?)x.QuantityOnHand).FirstOrDefaultAsync() ?? 0;
            }

            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/stock/adjustments", new
                {
                    productVariantId = variantId, locationId, quantityDelta = 1
                })).StatusCode);
            var response = await client.PostAsJsonAsync("/stock/adjustments", new
            {
                productVariantId = variantId, locationId,
                quantityDelta = 1, reason = "Count correction"
            });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var history = await client.GetFromJsonAsync<JsonElement>(
                $"/audit/entries?action=StockAdjusted&entityId={variantId}");
            Assert.Equal(1, history.GetProperty("totalItems").GetInt64());
            var entry = Assert.Single(history.GetProperty("items").EnumerateArray());
            Assert.Equal(actor, entry.GetProperty("userId").GetGuid());
            Assert.Equal("ProductVariant", entry.GetProperty("entityType").GetString());
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("correlationId").GetString()));
            Assert.Equal(before, JsonDocument.Parse(entry.GetProperty("oldValues").GetString()!)
                .RootElement.GetProperty("quantityOnHand").GetInt32());
            Assert.Equal(before + 1, JsonDocument.Parse(entry.GetProperty("newValues").GetString()!)
                .RootElement.GetProperty("quantityOnHand").GetInt32());
            Assert.DoesNotContain("Admin123!", entry.ToString());

            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.GetAsync("/audit/entries?pageSize=101")).StatusCode);
        });
    }
}
