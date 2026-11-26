using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplia.Audit.Infrastructure;
using Simplia.Audit.UseCases;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Identity.Infrastructure;
using Simplia.Identity.Infrastructure.Data;
using Simplia.Identity.UseCases.Auth.Abstractions;
using Xunit;

namespace Simplia.Audit.Tests;

public sealed class LoginHistoryIntegrationTests
{
    [Fact]
    public async Task Password_attempts_are_append_only_and_success_can_be_audited()
    {
        var baseConnection = Environment.GetEnvironmentVariable(
            "SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaLoginTest_{Guid.NewGuid():N}";
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
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = "Local-Test-Key-Must-Be-At-Least-32-Characters",
                    ["Jwt:Issuer"] = "Simplia",
                    ["Jwt:Audience"] = "Simplia"
                }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<ITenantContext>(new TestTenant(connection));
            var identity = new IdentityModuleInstaller();
            var audit = new AuditModuleInstaller();
            identity.Register(services, connection);
            audit.Register(services, connection);
            await using var provider = services.BuildServiceProvider();
            await identity.MigrateAsync(provider, CancellationToken.None);
            await audit.MigrateAsync(provider, CancellationToken.None);
            await identity.SeedAsync(provider, CancellationToken.None);

            await using var scope = provider.CreateAsyncScope();
            var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthService>();
            var reader = scope.ServiceProvider.GetRequiredService<ILoginAttemptReader>();
            var trail = scope.ServiceProvider.GetRequiredService<IAuditTrail>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            Assert.Null(await auth.LoginAsync("unknown@local.test", "wrong-password",
                "127.0.0.1", "Postman", CancellationToken.None));
            Assert.Null(await auth.LoginAsync("admin@dev.local", "wrong-password",
                "127.0.0.1", "Mobile App", CancellationToken.None));
            var result = await auth.LoginAsync("admin@dev.local", "Admin123!",
                "127.0.0.1", "Mobile App", CancellationToken.None);
            Assert.NotNull(result);

            var known = await reader.QueryAsync(result.UserId, null, null, null,
                null, 1, 10, CancellationToken.None);
            Assert.Equal(2, known.TotalItems);
            Assert.True(known.Items[0].Succeeded);
            Assert.False(known.Items[1].Succeeded);
            Assert.Equal("Mobile App", known.Items[0].UserAgent);
            Assert.Equal("127.0.0.1", known.Items[0].IpAddress);
            var unknown = await reader.QueryAsync(null, "unknown@local.test", false,
                null, null, 1, 10, CancellationToken.None);
            Assert.Equal(1, unknown.TotalItems);
            Assert.Null(unknown.Items[0].UserId);

            await trail.AppendAsync(result.UserId, "UserLoggedIn", "User",
                result.UserId, null,
                new Dictionary<string, object?> { ["method"] = "Password" },
                "login-request", CancellationToken.None);
            var auditHistory = await trail.QueryAsync(result.UserId, "UserLoggedIn",
                "User", result.UserId, null, null, 1, 10, CancellationToken.None);
            Assert.Equal(1, auditHistory.TotalItems);
            Assert.DoesNotContain("Admin123!", auditHistory.Items[0].NewValues);
            Assert.DoesNotContain(result.RefreshToken, auditHistory.Items[0].NewValues);

            var stored = await db.LoginAttempts.FirstAsync();
            db.LoginAttempts.Remove(stored);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>()
                    .UseSqlServer(connection).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) =>
            moduleName is "Identity" or "Audit";
    }
}
