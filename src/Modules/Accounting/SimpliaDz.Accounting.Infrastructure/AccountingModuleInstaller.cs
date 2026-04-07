using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Accounting.Infrastructure.Data;

namespace SimpliaDz.Accounting.Infrastructure;

public sealed class AccountingModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Accounting";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AccountingDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AccountingDbContext).Assembly.FullName);
            }));
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
