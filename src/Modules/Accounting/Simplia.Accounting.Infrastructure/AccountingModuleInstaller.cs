using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Accounting.Infrastructure.Data;
using Simplia.Accounting.Infrastructure.Services;
using Simplia.Accounting.UseCases.Services;

namespace Simplia.Accounting.Infrastructure;

public sealed class AccountingModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Accounting";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AccountingDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AccountingDbContext).Assembly.FullName);
            }));
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IExpenseTotalReader, ExpenseTotalReader>();
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
