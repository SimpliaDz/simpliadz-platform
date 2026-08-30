using Microsoft.Extensions.Options;
using SimpliaDz.BuildingBlocks.Tenancy.Configuration;
using SimpliaDz.BuildingBlocks.Tenancy.Resolution;
using Xunit;

namespace SimpliaDz.ModuleCatalog.Tests;

public sealed class TenancyConfigurationTests
{
    [Fact]
    public void ApiResolver_UsesConfiguredActiveTenant()
    {
        var resolver = new ConfigurationTenantResolver(Options.Create(new TenancyOptions
        {
            ActiveTenantId = " demo "
        }));

        Assert.Equal("demo", resolver.ResolveTenantId());
    }

    [Fact]
    public void ApiResolver_RejectsMissingActiveTenant()
    {
        var action = () => new ConfigurationTenantResolver(Options.Create(new TenancyOptions()));

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("ActiveTenantId", exception.Message);
    }

    [Fact]
    public void CliResolver_UsesTenantArgument()
    {
        var resolver = new CliTenantResolver(["--tenant", " demo "]);

        Assert.Equal("demo", resolver.ResolveTenantId());
    }

    [Fact]
    public void Store_ReturnsRequestedTenantAndItsOwnDatabase()
    {
        var options = new TenancyOptions
        {
            Tenants = new Dictionary<string, TenantOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["dev"] = new()
                {
                    Name = "Development",
                    ConnectionString = "Database=SimpliaDz_Dev",
                    Modules = new Dictionary<string, bool> { ["Identity"] = true }
                },
                ["demo"] = new()
                {
                    Name = "Demo",
                    ConnectionString = "Database=SimpliaDz_Demo",
                    Modules = new Dictionary<string, bool> { ["Inventory"] = true }
                }
            }
        };
        var store = new ConfigurationTenantStore(Options.Create(options));

        var tenant = store.GetTenant("DEMO");

        Assert.Equal("DEMO", tenant.TenantId);
        Assert.Equal("Database=SimpliaDz_Demo", tenant.ConnectionString);
        Assert.True(tenant.Modules["Inventory"]);
        Assert.False(tenant.Modules.ContainsKey("Identity"));
    }
}
