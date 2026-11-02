using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Purchasing.Infrastructure.Data;
using SimpliaDz.Purchasing.Infrastructure.Services;
using SimpliaDz.Purchasing.UseCases.Services;

namespace SimpliaDz.Purchasing.Infrastructure;

public sealed class PurchasingModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Purchasing";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PurchasingDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(PurchasingDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "purchasing");
            }));
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PurchasingDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
