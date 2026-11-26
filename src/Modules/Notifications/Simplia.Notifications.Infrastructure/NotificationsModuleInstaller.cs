using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Notifications.Infrastructure.Data;

namespace Simplia.Notifications.Infrastructure;

public sealed class NotificationsModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Notifications";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<NotificationsDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(NotificationsDbContext).Assembly.FullName);
            }));
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
