using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.Identity.UseCases.Users.Abstractions;
using Xunit;

namespace SimpliaDz.Api.Tests;

public sealed class BrandEndpointIntegrationTests
{
    [Fact]
    public async Task Brand_http_actions_validate_authorization_and_preserve_deactivated_records()
    {
        await ApiTestHost.RunAsync(async (client, services) =>
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.GetAsync("/brands")).StatusCode);
            await ApiTestHost.LoginAsAdminAsync(client);

            var invalid = await client.PostAsJsonAsync("/brands", new { name = " " });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Contains("Name", await invalid.Content.ReadAsStringAsync());

            var created = await client.PostAsJsonAsync("/brands", new { name = "Test Brand" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PostAsJsonAsync("/brands", new { name = "Test Brand" }))
                .StatusCode);
            Assert.Equal("Test Brand", (await client.GetFromJsonAsync<JsonElement>(
                $"/brands/{id}")).GetProperty("name").GetString());
            var updated = await client.PutAsJsonAsync($"/brands/{id}",
                new { name = "Updated Brand" });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            Assert.Contains("Updated Brand", await client.GetStringAsync("/brands"));
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PutAsync($"/brands/{id}/deactivate", null)).StatusCode);
            Assert.False((await client.GetFromJsonAsync<JsonElement>($"/brands/{id}"))
                .GetProperty("isActive").GetBoolean());
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/brands/{Guid.NewGuid()}")).StatusCode);

            using (var scope = services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();
                await users.CreateUserAsync("limited@test.local", "Limited", "User",
                    "LimitedPassword123!", null, CancellationToken.None);
            }
            client.DefaultRequestHeaders.Authorization = null;
            var login = await client.PostAsJsonAsync("/auth/login", new
                { email = "limited@test.local", password = "LimitedPassword123!" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("accessToken").GetString();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync("/brands")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync("/brands", new { name = "Blocked Brand" }))
                .StatusCode);
        });
    }
}
