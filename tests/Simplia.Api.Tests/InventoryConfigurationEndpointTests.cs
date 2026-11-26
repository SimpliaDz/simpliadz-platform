using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Simplia.Inventory.Infrastructure.Data;
using Xunit;

namespace Simplia.Api.Tests;

public sealed class InventoryConfigurationEndpointTests
{
    [Fact]
    public async Task Barcode_label_templates_can_be_configured_and_inactive_templates_cannot_print()
    {
        await ApiTestHost.RunAsync(async (client, services) =>
        {
            await ApiTestHost.LoginAsAdminAsync(client);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/barcode-label-templates", new
                {
                    name = "Invalid dimensions", pageWidthMm = 10m,
                    pageHeightMm = 297m, labelWidthMm = 90m, labelHeightMm = 40m,
                    marginMm = 5m, gapMm = 3m, showName = true, showSku = true
                })).StatusCode);
            var request = new
            {
                name = "Custom barcode labels", pageWidthMm = 210m, pageHeightMm = 297m,
                labelWidthMm = 90m, labelHeightMm = 40m, marginMm = 5m, gapMm = 3m,
                showName = true, showSku = true
            };
            var created = await client.PostAsJsonAsync("/barcode-label-templates", request);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
            Assert.Contains("Custom barcode labels",
                await client.GetStringAsync("/barcode-label-templates"));
            var updated = await client.PutAsJsonAsync($"/barcode-label-templates/{id}",
                new
                {
                    name = "Small barcode labels", pageWidthMm = 210m,
                    pageHeightMm = 297m, labelWidthMm = 60m, labelHeightMm = 30m,
                    marginMm = 5m, gapMm = 3m, showName = false, showSku = true
                });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            Assert.Equal("Small barcode labels",
                (await updated.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("name").GetString());
            Guid variantId;
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                variantId = await db.ProductVariants.AsNoTracking()
                    .Where(x => x.Barcode != null).Select(x => x.Id).FirstAsync();
            }
            var print = await client.PostAsJsonAsync("/barcode-labels/print-document", new
            {
                templateId = id,
                lines = new[] { new { variantId, quantity = 2 } }
            });
            Assert.Equal(HttpStatusCode.OK, print.StatusCode);
            Assert.Contains("<svg", await print.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PutAsync($"/barcode-label-templates/{id}/deactivate", null))
                .StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PutAsJsonAsync($"/barcode-label-templates/{id}", request))
                .StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PostAsJsonAsync("/barcode-labels/print-document", new
                {
                    templateId = id,
                    lines = new[] { new { variantId = Guid.NewGuid(), quantity = 1 } }
                })).StatusCode);
        });
    }

    [Fact]
    public async Task Inventory_document_templates_validate_type_and_preserve_deactivated_configuration()
    {
        await ApiTestHost.RunAsync(async (client, _) =>
        {
            await ApiTestHost.LoginAsAdminAsync(client);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/inventory-document-templates", new
                {
                    name = "Bad", type = "Unknown", title = "Bad",
                    columns = new[] { "Sku" }, paperSize = "A4",
                    landscape = false, footerText = ""
                })).StatusCode);
            var created = await client.PostAsJsonAsync("/inventory-document-templates", new
            {
                name = "Custom stock list", type = "StockList", title = "Stock",
                columns = new[] { "Product", "Sku", "Quantity" }, paperSize = "A4",
                landscape = false, footerText = ""
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
            Assert.Contains("Custom stock list",
                await client.GetStringAsync("/inventory-document-templates"));
            var updated = await client.PutAsJsonAsync($"/inventory-document-templates/{id}",
                new
                {
                    name = "Updated stock list", title = "Current stock",
                    columns = new[] { "Sku", "Quantity" }, paperSize = "A3",
                    landscape = true, footerText = "Approved"
                });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            Assert.Equal("Updated stock list",
                (await updated.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("name").GetString());
            var print = await client.GetAsync($"/reports/inventory/document?templateId={id}");
            Assert.Equal(HttpStatusCode.OK, print.StatusCode);
            Assert.Contains("Current stock", await print.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PutAsync($"/inventory-document-templates/{id}/deactivate",
                    null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PutAsJsonAsync($"/inventory-document-templates/{id}",
                    new
                    {
                        name = "Cannot edit", title = "Stock",
                        columns = new[] { "Sku" }, paperSize = "A4",
                        landscape = false, footerText = ""
                    })).StatusCode);
        });
    }

    [Fact]
    public async Task Warning_settings_can_be_configured_and_invalid_filters_are_rejected()
    {
        await ApiTestHost.RunAsync(async (client, _) =>
        {
            await ApiTestHost.LoginAsAdminAsync(client);
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync("/inventory-warnings/settings")).StatusCode);
            var response = await client.PutAsJsonAsync("/inventory-warnings/settings",
                new
                {
                    enabled = true, lowStockEnabled = true, ageingEnabled = false,
                    intervalMinutes = 60, ageThresholdDays = 90
                });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var settings = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(settings.GetProperty("ageingEnabled").GetBoolean());
            Assert.Equal(60, settings.GetProperty("intervalMinutes").GetInt32());
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsync("/inventory-warnings/run", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.GetAsync("/inventory-warnings?kind=invalid")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync("/inventory-warnings?activeOnly=true")).StatusCode);
        });
    }
}
