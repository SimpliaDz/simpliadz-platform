using SimpliaDz.ModuleCatalog;
using Xunit;

namespace SimpliaDz.ModuleCatalog.Tests;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void GetModules_returns_every_installed_module_once()
    {
        var moduleNames = ModuleCatalog.GetModules()
            .Select(module => module.ModuleName)
            .ToArray();

        Assert.Equal(
        [
            "Identity",
            "Inventory",
            "Sales",
            "Purchasing",
            "Accounting",
            "Notifications",
            "Reporting",
            "Audit"
        ],
        moduleNames);

        Assert.Equal(moduleNames.Length, moduleNames.Distinct(StringComparer.Ordinal).Count());
    }
}
