using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.Infrastructure.Services;
using SimpliaDz.Sales.UseCases.Services;

namespace SimpliaDz.Sales.Infrastructure;

public sealed class SalesModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Sales";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SalesDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(SalesDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales");
            }));
        services.AddScoped<ICustomerService, CustomerService>();
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
