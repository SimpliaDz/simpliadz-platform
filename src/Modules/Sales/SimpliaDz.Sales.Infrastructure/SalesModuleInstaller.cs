using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Sales.Infrastructure.Data;

namespace SimpliaDz.Sales.Infrastructure;

public sealed class SalesModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Sales";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SalesDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(SalesDbContext).Assembly.FullName);
            }));
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
