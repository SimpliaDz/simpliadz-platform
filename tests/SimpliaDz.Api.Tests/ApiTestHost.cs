using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SimpliaDz.Api.Configuration;
using SimpliaDz.Audit.Infrastructure;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Identity.Infrastructure;
using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Inventory.Infrastructure;

namespace SimpliaDz.Api.Tests;

internal static class ApiTestHost
{
    public static async Task RunAsync(Func<HttpClient, IServiceProvider, Task> scenario)
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzApiTest_{Guid.NewGuid():N}";
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
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = "Local-Test-Key-Must-Be-At-Least-32-Characters",
                    ["Jwt:Issuer"] = "SimpliaDz",
                    ["Jwt:Audience"] = "SimpliaDz"
                }).Build());
            services.AddSingleton<ITenantContext>(new TestTenant(connection));
            var identity = new IdentityModuleInstaller();
            var inventory = new InventoryModuleInstaller();
            var audit = new AuditModuleInstaller();
            identity.Register(services, connection);
            inventory.Register(services, connection);
            audit.Register(services, connection);
            await using (var provider = services.BuildServiceProvider())
            {
                await identity.MigrateAsync(provider, CancellationToken.None);
                await inventory.MigrateAsync(provider, CancellationToken.None);
                await audit.MigrateAsync(provider, CancellationToken.None);
                await identity.SeedAsync(provider, CancellationToken.None);
                await inventory.SeedAsync(provider, CancellationToken.None);
            }

            await using var factory = new WebApplicationFactory<JwtOptions>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Tenancy:ActiveTenantId"] = "api-test",
                            ["Tenancy:Tenants:api-test:Name"] = "Functional test",
                            ["Tenancy:Tenants:api-test:ConnectionString"] = connection,
                            ["Tenancy:Tenants:api-test:Modules:Identity"] = "true",
                            ["Tenancy:Tenants:api-test:Modules:Inventory"] = "true",
                            ["Tenancy:Tenants:api-test:Modules:Audit"] = "true"
                        }));
                    builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
                });
            using var client = factory.CreateClient();
            await scenario(client, factory.Services);
        }
        finally
        {
            await using var cleanup = new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>()
                    .UseSqlServer(connection).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    public static async Task LoginAsAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new
            { email = "admin@dev.local", password = "Admin123!" });
        if (response.StatusCode != HttpStatusCode.OK)
            throw new Exception($"Admin login failed with {(int)response.StatusCode}.");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers
            .AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "api-test";
        public string Name => "api-test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) =>
            moduleName is "Identity" or "Inventory" or "Audit";
    }
}
