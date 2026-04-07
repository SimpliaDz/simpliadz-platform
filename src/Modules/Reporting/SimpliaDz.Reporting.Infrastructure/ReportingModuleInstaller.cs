using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Reporting.Infrastructure.Data;

namespace SimpliaDz.Reporting.Infrastructure;

public sealed class ReportingModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Reporting";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReportingDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
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
