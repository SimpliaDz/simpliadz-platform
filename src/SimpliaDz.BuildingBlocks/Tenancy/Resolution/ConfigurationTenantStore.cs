using Microsoft.Extensions.Options;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Configuration;

namespace SimpliaDz.BuildingBlocks.Tenancy.Resolution;

public sealed class ConfigurationTenantStore : ITenantStore
{
    private readonly TenancyOptions _options;

    public ConfigurationTenantStore(IOptions<TenancyOptions> options) => _options = options.Value;

    public TenantDescriptor GetTenant(string tenantId)
    {
        if (!_options.Tenants.TryGetValue(tenantId, out var t))
            throw new InvalidOperationException($"Unknown tenant '{tenantId}'.");

        return new TenantDescriptor(
            TenantId: tenantId,
            Name: t.Name,
            ConnectionString: t.ConnectionString,
            Modules: t.Modules);
    }
}
