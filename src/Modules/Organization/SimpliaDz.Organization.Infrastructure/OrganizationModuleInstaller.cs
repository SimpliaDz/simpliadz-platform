using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Organization.Infrastructure.Data;
using SimpliaDz.Organization.Infrastructure.Data.Repositories;
using SimpliaDz.Organization.UseCases.Abstractions;

namespace SimpliaDz.Organization.Infrastructure;

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
