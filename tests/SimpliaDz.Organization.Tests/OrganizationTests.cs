using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Organization.Infrastructure;
using SimpliaDz.Organization.Infrastructure.Data;
using SimpliaDz.Organization.UseCases.Abstractions;
using SimpliaDz.Organization.UseCases.Commands.UpdateOrganization;
using SimpliaDz.Organization.UseCases.Queries.GetOrganization;
using SimpliaDz.BuildingBlocks.Exceptions;
using Xunit;
using OrganizationEntity = SimpliaDz.Organization.Core.Entities.Organization;

namespace SimpliaDz.Organization.Tests;

public sealed class OrganizationTests
{
    [Fact]
    public void New_organization_defaults_currency_to_DZD()
    {
        var organization = CreateOrganization(currencyCode: null);

        Assert.Equal("DZD", organization.CurrencyCode);
        Assert.Equal("Africa/Algiers", organization.TimeZoneId);
        Assert.Equal(OrganizationEntity.ProfileId, organization.Id);
    }

    [Fact]
    public void Organization_normalizes_currency_code()
    {
        var organization = CreateOrganization(currencyCode: "dzd");

        Assert.Equal("DZD", organization.CurrencyCode);
    }

    [Fact]
    public void Organization_rejects_unknown_time_zone()
    {
        var organization = CreateOrganization(currencyCode: null);
        Assert.Throws<ArgumentException>(() => organization.Update("Store", null,
            null, null, null, "DZD", null, true, "Not/A_Time_Zone"));
    }

    [Fact]
    public async Task Validator_rejects_missing_name_and_invalid_currency()
    {
        var validator = new UpdateOrganizationValidator();
        var command = CreateCommand(name: "", currencyCode: "DZ");

        var result = await validator.ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.CurrencyCode));
    }

    [Fact]
    public async Task Update_handler_creates_then_updates_the_single_profile()
    {
        var repository = new InMemoryOrganizationRepository();
        var handler = new UpdateOrganizationHandler(repository);
        var reader = new GetOrganizationHandler(repository);

        await Assert.ThrowsAsync<NotFoundException>(() => reader.Handle(
            new GetOrganizationQuery(), CancellationToken.None).AsTask());

        var created = await handler.Handle(CreateCommand("First name", null), CancellationToken.None);
        var updated = await handler.Handle(CreateCommand("Updated name", "CAD"), CancellationToken.None);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated name", updated.Name);
        Assert.Equal("CAD", updated.CurrencyCode);
        Assert.Equal("Updated name", (await reader.Handle(new GetOrganizationQuery(),
            CancellationToken.None)).Name);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public void DbContext_uses_the_current_tenant_connection_string()
    {
        const string tenantConnectionString =
            "Server=tenant-server;Database=Tenant_Organization;User Id=test;Password=test;TrustServerCertificate=True;";

        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TestTenantContext(tenantConnectionString));
        new OrganizationModuleInstaller().Register(services, "ignored-startup-connection");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();

        var actualConnectionString = db.Database.GetConnectionString();
        Assert.NotNull(actualConnectionString);

        var expected = new SqlConnectionStringBuilder(tenantConnectionString);
        var actual = new SqlConnectionStringBuilder(actualConnectionString);

        Assert.Equal(expected.DataSource, actual.DataSource);
        Assert.Equal(expected.InitialCatalog, actual.InitialCatalog);
        Assert.Equal(expected.UserID, actual.UserID);
    }

    private static OrganizationEntity CreateOrganization(string? currencyCode) =>
        new(
            "SimpliaDz Store",
            null,
            null,
            null,
            null,
            currencyCode,
            null,
            true);

    private static UpdateOrganizationCommand CreateCommand(string name, string? currencyCode) =>
        new(name, null, null, null, null, currencyCode, null, true);

    private sealed class InMemoryOrganizationRepository : IOrganizationRepository
    {
        private OrganizationEntity? _organization;

        public int AddCount { get; private set; }
        public int SaveCount { get; private set; }

        public Task<OrganizationEntity?> GetAsync(CancellationToken ct) =>
            Task.FromResult(_organization);

        public Task AddAsync(OrganizationEntity organization, CancellationToken ct)
        {
            _organization = organization;
            AddCount++;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
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
