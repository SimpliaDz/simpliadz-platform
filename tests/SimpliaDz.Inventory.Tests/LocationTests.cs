using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.UseCases.Locations.Abstractions;
using SimpliaDz.Inventory.UseCases.Locations.Commands.CreateLocation;
using SimpliaDz.Inventory.UseCases.Locations.Commands.DeactivateLocation;
using SimpliaDz.Inventory.UseCases.Locations.Commands.UpdateLocation;
using SimpliaDz.SharedKernel.Authorization;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class LocationTests
{
    [Fact]
    public void Location_normalizes_values_and_records_creation_time()
    {
        var before = DateTimeOffset.UtcNow;
        var location = new InventoryLocation(
            "  Central Store  ",
            "  alg-01 ",
            LocationType.Store,
            "  Algiers  ",
            "  0550 00 00 00  ");

        Assert.Equal("Central Store", location.Name);
        Assert.Equal("ALG-01", location.Code);
        Assert.Equal(LocationType.Store, location.Type);
        Assert.Equal("Algiers", location.Address);
        Assert.Equal("0550 00 00 00", location.Phone);
        Assert.True(location.IsActive);
        Assert.True(location.CreatedAt >= before);
    }

    [Fact]
    public async Task Validator_rejects_missing_name_and_unsupported_type()
    {
        var validator = new CreateLocationValidator();
        var result = await validator.ValidateAsync(
            new CreateLocationCommand("", "A", "Office", null, null));

        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        Assert.Contains(result.Errors, error => error.PropertyName == "Type");
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code()
    {
        var repository = new InMemoryLocationRepository();
        repository.Seed(new InventoryLocation("Existing", "STORE-1"));
        var handler = new CreateLocationHandler(repository);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new CreateLocationCommand("Another", "store-1", "Store", null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task Update_changes_location_without_replacing_it()
    {
        var repository = new InMemoryLocationRepository();
        var location = new InventoryLocation("Old", "OLD");
        repository.Seed(location);
        var handler = new UpdateLocationHandler(repository);

        var response = await handler.Handle(
            new UpdateLocationCommand(location.Id, "Warehouse", "WH-1", "Warehouse", "Oran", "0123"),
            CancellationToken.None);

        Assert.Equal(location.Id, response.Id);
        Assert.Equal("WH-1", response.Code);
        Assert.Equal("Warehouse", response.Type);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public async Task Deactivate_keeps_the_location_for_historical_references()
    {
        var repository = new InMemoryLocationRepository();
        var location = new InventoryLocation("Store", "STORE");
        repository.Seed(location);
        var handler = new DeactivateLocationHandler(repository);

        await handler.Handle(new DeactivateLocationCommand(location.Id), CancellationToken.None);

        Assert.False(location.IsActive);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public void Inventory_context_uses_the_current_tenant_connection()
    {
        const string tenantConnection =
            "Server=tenant-server;Database=Tenant_Inventory;User Id=test;Password=test;TrustServerCertificate=True;";

        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TestTenantContext(tenantConnection));
        new InventoryModuleInstaller().Register(services, "ignored-startup-connection");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var actualConnection = db.Database.GetConnectionString();

        Assert.NotNull(actualConnection);
        var expected = new SqlConnectionStringBuilder(tenantConnection);
        var actual = new SqlConnectionStringBuilder(actualConnection);
        Assert.Equal(expected.DataSource, actual.DataSource);
        Assert.Equal(expected.InitialCatalog, actual.InitialCatalog);
    }

    [Fact]
    public void Permission_catalog_contains_unique_feature_permissions()
    {
        Assert.Contains(PermissionNames.Locations.View, PermissionNames.All);
        Assert.Contains(PermissionNames.Locations.Create, PermissionNames.All);
        Assert.Contains(PermissionNames.Locations.Edit, PermissionNames.All);
        Assert.Contains(PermissionNames.Organization.View, PermissionNames.All);
        Assert.Contains(PermissionNames.Organization.Manage, PermissionNames.All);
        Assert.Equal(PermissionNames.All.Count, PermissionNames.All.Distinct().Count());
    }

    private sealed class InMemoryLocationRepository : ILocationRepository
    {
        private readonly List<InventoryLocation> _locations = [];

        public int Count => _locations.Count;

        public void Seed(InventoryLocation location) => _locations.Add(location);

        public Task<InventoryLocation?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_locations.FirstOrDefault(location => location.Id == id));

        public Task<IReadOnlyList<InventoryLocation>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<InventoryLocation>>(_locations);

        public Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_locations.Any(location =>
                location.Code == code && (!excludingId.HasValue || location.Id != excludingId.Value)));

        public Task<bool> IsActiveAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_locations.Any(location => location.Id == id && location.IsActive));

        public Task AddAsync(InventoryLocation location, CancellationToken ct)
        {
            _locations.Add(location);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(string connectionString) => ConnectionString = connectionString;

        public string TenantId => "tenant-test";
        public string Name => "Tenant Test";
        public string ConnectionString { get; }
        public bool IsModuleEnabled(string moduleName) => true;
    }
}
