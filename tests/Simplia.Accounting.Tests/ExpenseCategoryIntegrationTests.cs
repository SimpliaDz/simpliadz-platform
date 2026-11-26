using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Simplia.Accounting.Infrastructure;
using Simplia.Accounting.Infrastructure.Data;
using Simplia.Accounting.UseCases.Services;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Xunit;

namespace Simplia.Accounting.Tests;

public sealed class ExpenseCategoryIntegrationTests
{
    [Fact]
    public async Task Categories_are_managed_per_tenant_database_and_retained_after_deactivation()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var firstName = $"SimpliaExpenseCatA_{Guid.NewGuid():N}";
        var secondName = $"SimpliaExpenseCatB_{Guid.NewGuid():N}";
        var firstConnection = ForDatabase(baseConnection, firstName);
        var secondConnection = ForDatabase(baseConnection, secondName);
        await using (var master = new SqlConnection(ForDatabase(baseConnection, "master")))
        {
            await master.OpenAsync();
            foreach (var name in new[] { firstName, secondName })
            {
                await using var command = new SqlCommand($"CREATE DATABASE [{name}]", master);
                await command.ExecuteNonQueryAsync();
            }
        }

        try
        {
            using var firstProvider = NewServices(firstConnection);
            using var secondProvider = NewServices(secondConnection);
            using var firstScope = firstProvider.CreateScope();
            using var secondScope = secondProvider.CreateScope();
            var firstDb = firstScope.ServiceProvider.GetRequiredService<AccountingDbContext>();
            var secondDb = secondScope.ServiceProvider.GetRequiredService<AccountingDbContext>();
            await firstDb.Database.MigrateAsync();
            await secondDb.Database.MigrateAsync();
            var first = firstScope.ServiceProvider.GetRequiredService<IExpenseCategoryService>();
            var second = secondScope.ServiceProvider.GetRequiredService<IExpenseCategoryService>();

            var created = await first.CreateAsync("  Rent  ", CancellationToken.None);
            Assert.Equal("Rent", created.Name);
            Assert.Empty(await second.ListAsync(null, CancellationToken.None));
            await Assert.ThrowsAsync<NotFoundException>(() => second.GetByIdAsync(created.Id,
                CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() => first.CreateAsync("Rent", CancellationToken.None));
            var updated = await first.UpdateAsync(created.Id, "Utilities", CancellationToken.None);
            Assert.Equal(created.Id, updated.Id);
            await first.DeactivateAsync(created.Id, CancellationToken.None);
            Assert.False((await first.GetByIdAsync(created.Id, CancellationToken.None)).IsActive);
            Assert.Single(await first.ListAsync(false, CancellationToken.None));
            Assert.Empty(await first.ListAsync(true, CancellationToken.None));
            Assert.Empty(await second.ListAsync(null, CancellationToken.None));
            Assert.Equal(created.Id, (await first.GetByIdAsync(created.Id,
                CancellationToken.None)).Id);
            await Assert.ThrowsAsync<ConflictException>(() => first.CreateAsync("Utilities", CancellationToken.None));
        }
        finally
        {
            await using var firstCleanup = NewDb(firstConnection);
            await firstCleanup.Database.EnsureDeletedAsync();
            await using var secondCleanup = NewDb(secondConnection);
            await secondCleanup.Database.EnsureDeletedAsync();
        }
    }

    private static string ForDatabase(string connection, string database) =>
        new SqlConnectionStringBuilder(connection) { InitialCatalog = database }.ConnectionString;

    private static AccountingDbContext NewDb(string connection) => new(
        new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "accounting"))
            .Options);

    private static ServiceProvider NewServices(string connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TestTenant(connection));
        new AccountingModuleInstaller().Register(services, connection);
        return services.BuildServiceProvider();
    }

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName == "Accounting";
    }
}
