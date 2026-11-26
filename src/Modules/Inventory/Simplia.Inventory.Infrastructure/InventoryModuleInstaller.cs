using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Inventory.Infrastructure.Data.Repositories;
using Simplia.Inventory.Infrastructure.Data.Seeders;
using Simplia.Inventory.Infrastructure.Services;
using Simplia.Inventory.UseCases.Locations.Abstractions;
using Simplia.Inventory.UseCases.Categories.Abstractions;
using Simplia.Inventory.UseCases.Brands.Abstractions;
using Simplia.Inventory.UseCases.Attributes.Abstractions;
using Simplia.Inventory.UseCases.Prices.Abstractions;
using Simplia.Inventory.UseCases.Products.Abstractions;
using Simplia.Inventory.UseCases.Services;

namespace Simplia.Inventory.Infrastructure;

public sealed class InventoryModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Inventory";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<InventoryDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString, sql =>
            {
                // Keep migrations inside the Inventory.Infrastructure assembly
                sql.MigrationsAssembly(typeof(InventoryDbContext).Assembly.FullName);
            }));

        // Repository registrations (per-aggregate root)
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<ICategoryRepository, EfCategoryRepository>();
        services.AddScoped<IBrandRepository, EfBrandRepository>();
        services.AddScoped<IAttributeRepository, EfAttributeRepository>();
        services.AddScoped<IPriceListRepository, EfPriceListRepository>();
        services.AddScoped<ILocationRepository, EfLocationRepository>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IInventoryPolicyService, InventoryPolicyService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IInventoryCountService, InventoryCountService>();
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
    }

    public async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // IMPORTANT:
        // - Idempotent
        // - Safe to run multiple times
        // - No assumptions about existing data

        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        // Example placeholder (we'll add real seeds later):
        // if (!await db.Products.AnyAsync(cancellationToken))
        // {
        //     db.Products.Add(new Product("Sample"));
        //     await db.SaveChangesAsync(cancellationToken);
        // }

        await InventorySeeder.SeedAsync(db, cancellationToken);
    }
}
