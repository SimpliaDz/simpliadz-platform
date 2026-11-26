using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Organization.Infrastructure.Data;
using Simplia.Organization.Infrastructure.Data.Repositories;
using Simplia.Organization.UseCases.Abstractions;

namespace Simplia.Organization.Infrastructure;

public sealed class OrganizationModuleInstaller : IModuleInstaller
{
    public string ModuleName => "Organization";

    public void Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OrganizationDbContext>((sp, options) =>
            options.UseSqlServer(
                sp.GetRequiredService<ITenantContext>().ConnectionString,
                sql =>
                {
                    sql.MigrationsAssembly(typeof(OrganizationDbContext).Assembly.FullName);
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "organization");
                }));

        services.AddScoped<IOrganizationRepository, EfOrganizationRepository>();
    }

    public async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
