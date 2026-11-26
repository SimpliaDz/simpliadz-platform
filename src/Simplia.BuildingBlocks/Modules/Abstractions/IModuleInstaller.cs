using Microsoft.Extensions.DependencyInjection;

namespace Simplia.BuildingBlocks.Modules.Abstractions;

public interface IModuleInstaller
{
    /// <summary>
    /// Logical name used in tenant configuration (e.g. "Inventory", "Billing")
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Register module services (DbContext, repositories, etc.)
    /// using the tenant-specific connection string.
    /// </summary>
    void Register(IServiceCollection services, string connectionString);

    /// <summary>
    /// Apply EF Core migrations for this module.
    /// Must be idempotent.
    /// </summary>
    Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);

    /// <summary>
    /// Seed base data for this module (idempotent).
    /// </summary>
    Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
