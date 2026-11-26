using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Reporting.Infrastructure.Data;

namespace Simplia.Reporting.Infrastructure;

public sealed class ReportingModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Reporting";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReportingDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ReportingDbContext).Assembly.FullName);
            }));
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
