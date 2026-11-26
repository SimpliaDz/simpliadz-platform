using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Configuration;
using Simplia.BuildingBlocks.Tenancy.Resolution;

namespace Simplia.BuildingBlocks.Tenancy;

public static class TenancyServiceCollectionExtensions
{
    public static IServiceCollection AddTenancyFromConfiguration(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<TenancyOptions>(config.GetSection("Tenancy"));
        services.AddSingleton<ITenantStore, ConfigurationTenantStore>();

        // ITenantResolver is host-specific (CLI vs HTTP), so host registers it.
        // Then we build TenantContext as Scoped:
        services.AddScoped<ITenantContext>(sp =>
        {
            var resolver = sp.GetRequiredService<ITenantResolver>();
            var store = sp.GetRequiredService<ITenantStore>();
            var tenantId = resolver.ResolveTenantId();
            var tenant = store.GetTenant(tenantId);
            return new TenantContext(tenant);
        });

        return services;
    }
}
