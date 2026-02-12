using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;

namespace SimpliaDz.Sales.Infrastructure;

public sealed class SalesModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Sales";

    public void Register(IServiceCollection services, string connectionString)
    {
        // No EF / no services yet for this module.
        // We'll add DbContext + EF packages when we start implementing the module.
    }

    public Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
