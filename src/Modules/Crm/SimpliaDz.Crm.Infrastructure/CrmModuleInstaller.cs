using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Crm.Infrastructure.Data;
using SimpliaDz.Crm.Infrastructure.Services;
using SimpliaDz.Crm.UseCases.Loyalty;

namespace SimpliaDz.Crm.Infrastructure;

public sealed class CrmModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Crm";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CrmDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString,
                sql =>
                {
                    sql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName);
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "crm");
                }));
        services.AddScoped<ILoyaltyService, LoyaltyService>();
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CrmDbContext>()
            .Database.MigrateAsync(ct);
    }

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken ct) =>
        Task.CompletedTask;
}
