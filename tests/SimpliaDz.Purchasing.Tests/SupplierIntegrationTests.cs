using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Purchasing.Infrastructure;
using SimpliaDz.Purchasing.Infrastructure.Data;
using SimpliaDz.Purchasing.UseCases.Services;
using Xunit;

namespace SimpliaDz.Purchasing.Tests;

public sealed class SupplierIntegrationTests
{
    [Fact]
    public async Task Supplier_can_be_created_searched_updated_and_deactivated_without_deletion()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzSupplierTest_{Guid.NewGuid():N}";
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
            services.AddSingleton<ITenantContext>(new TestTenant(connection));
            var installer = new PurchasingModuleInstaller();
            installer.Register(services, connection);
            await using var provider = services.BuildServiceProvider();
            await installer.MigrateAsync(provider, CancellationToken.None);
            await using var scope = provider.CreateAsyncScope();
            var suppliers = scope.ServiceProvider.GetRequiredService<ISupplierService>();
            var db = scope.ServiceProvider.GetRequiredService<PurchasingDbContext>();

            var created = await suppliers.CreateAsync("Fabric House", "0555123456",
                "contact@fabric.local", "Algiers", "Preferred supplier",
                CancellationToken.None);
            Assert.True(created.IsActive);
            Assert.Equal(created.Id, (await suppliers.GetByIdAsync(created.Id,
                CancellationToken.None)).Id);
            Assert.Single(await suppliers.SearchAsync("Fabric", true, 1, 20,
                CancellationToken.None));
            Assert.Single(await suppliers.SearchAsync("123456", true, 1, 20,
                CancellationToken.None));
            Assert.Single(await suppliers.SearchAsync("contact@", true, 1, 20,
                CancellationToken.None));

            var updated = await suppliers.UpdateAsync(created.Id, "New Fabric House",
                "0555123456", null, null, "Updated", CancellationToken.None);
            Assert.Equal("New Fabric House", updated.Name);
            Assert.Equal(created.Id, updated.Id);
            await suppliers.DeactivateAsync(created.Id, CancellationToken.None);
            Assert.False((await suppliers.GetByIdAsync(created.Id, CancellationToken.None))
                .IsActive);
            Assert.Empty(await suppliers.SearchAsync("Fabric", true, 1, 20,
                CancellationToken.None));
            Assert.Single(await suppliers.SearchAsync("Fabric", false, 1, 20,
                CancellationToken.None));
            Assert.NotNull(await db.Suppliers.AsNoTracking()
                .SingleAsync(x => x.Id == created.Id));
            await Assert.ThrowsAsync<NotFoundException>(() =>
                suppliers.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                suppliers.SearchAsync(null, null, 0, 20, CancellationToken.None));
        }
        finally
        {
            await using var cleanup = new PurchasingDbContext(
                new DbContextOptionsBuilder<PurchasingDbContext>()
                    .UseSqlServer(connection).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "supplier-test";
        public string Name => "supplier-test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName == "Purchasing";
    }
}
