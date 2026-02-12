using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;

namespace SimpliaDz.Purchasing.Infrastructure;

public sealed class PurchasingModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Purchasing";

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
