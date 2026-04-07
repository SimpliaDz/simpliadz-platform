using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Audit.Infrastructure.Data;

namespace SimpliaDz.Audit.Infrastructure;

public sealed class AuditModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Audit";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AuditDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName);
            }));
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
