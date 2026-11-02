using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.Infrastructure.Data.Repositories;
using SimpliaDz.Inventory.Infrastructure.Data.Seeders;
using SimpliaDz.Inventory.Infrastructure.Services;
using SimpliaDz.Inventory.UseCases.Locations.Abstractions;
using SimpliaDz.Inventory.UseCases.Categories.Abstractions;
using SimpliaDz.Inventory.UseCases.Brands.Abstractions;
using SimpliaDz.Inventory.UseCases.Attributes.Abstractions;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;
using SimpliaDz.Inventory.UseCases.Services;

namespace SimpliaDz.Inventory.Infrastructure;

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
        services.AddScoped<ILocationRepository, EfLocationRepository>();
        services.AddScoped<IStockService, StockService>();
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
