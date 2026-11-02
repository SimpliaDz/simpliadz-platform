using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.Infrastructure.Services;
using Xunit;

namespace SimpliaDz.Sales.Tests;

public sealed class CustomerIntegrationTests
{
    [Fact]
    public async Task Customers_search_by_name_and_phone_and_stay_in_their_tenant_database()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;

        var firstName = $"SimpliaDzCustomerA_{Guid.NewGuid():N}";
        var secondName = $"SimpliaDzCustomerB_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(baseConnection, firstName);
        await CreateDatabaseAsync(baseConnection, secondName);
        var firstConnection = ForDatabase(baseConnection, firstName);
        var secondConnection = ForDatabase(baseConnection, secondName);
        try
        {
            await using var first = NewDb(firstConnection);
            await using var second = NewDb(secondConnection);
            await first.Database.MigrateAsync();
            await second.Database.MigrateAsync();
            var service = new CustomerService(first);
            var customer = await service.CreateAsync("Amel Retail", "0550-123-456", null,
                null, CustomerType.Retail, null, CancellationToken.None);

            Assert.Single(await service.SearchAsync("Amel", null, null, 1, 50,
                CancellationToken.None));
            Assert.Single(await service.SearchAsync("123-456", null, null, 1, 50,
                CancellationToken.None));
            Assert.Empty(await service.SearchAsync("missing", null, null, 1, 50,
                CancellationToken.None));
            Assert.Empty(await new CustomerService(second).SearchAsync("Amel", null, null,
                1, 50, CancellationToken.None));

            await service.UpdateAsync(customer.Id, "Amel Wholesale", "0550-123-456", null,
                "Algiers", CustomerType.Wholesale, null, CancellationToken.None);
            Assert.Single(await service.SearchAsync("Amel", CustomerType.Wholesale, true,
                1, 50, CancellationToken.None));
            await service.DeactivateAsync(customer.Id, CancellationToken.None);
            var historical = await service.GetByIdAsync(customer.Id, CancellationToken.None);
            Assert.False(historical.IsActive);
            Assert.Equal(CustomerType.Wholesale.ToString(), historical.CustomerType);
            Assert.Single(await service.SearchAsync("123", null, false, 1, 50,
                CancellationToken.None));
        }
        finally
        {
            await using var firstCleanup = NewDb(firstConnection);
            await firstCleanup.Database.EnsureDeletedAsync();
            await using var secondCleanup = NewDb(secondConnection);
            await secondCleanup.Database.EnsureDeletedAsync();
        }
    }

    private static async Task CreateDatabaseAsync(string baseConnection, string name)
    {
        await using var master = new SqlConnection(ForDatabase(baseConnection, "master"));
        await master.OpenAsync();
        await using var command = new SqlCommand($"CREATE DATABASE [{name}]", master);
        await command.ExecuteNonQueryAsync();
    }

    private static string ForDatabase(string baseConnection, string name) =>
        new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = name }.ConnectionString;

    private static SalesDbContext NewDb(string connectionString) => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales"))
            .Options);
}
