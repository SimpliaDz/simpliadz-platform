using Microsoft.Extensions.Options;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Configuration;

namespace Simplia.BuildingBlocks.Tenancy.Resolution;

public sealed class ConfigurationTenantStore : ITenantStore
{
    private readonly TenancyOptions _options;

    public ConfigurationTenantStore(IOptions<TenancyOptions> options) => _options = options.Value;

    public TenantDescriptor GetTenant(string tenantId)
    {
        if (!_options.Tenants.TryGetValue(tenantId, out var t))
            throw new InvalidOperationException($"Unknown tenant '{tenantId}'.");

        if (string.IsNullOrWhiteSpace(t.Name))
            throw new InvalidOperationException($"Tenant '{tenantId}' must have a name.");

        if (string.IsNullOrWhiteSpace(t.ConnectionString))
            throw new InvalidOperationException($"Tenant '{tenantId}' must have a connection string.");

        return new TenantDescriptor(
            TenantId: tenantId,
            Name: t.Name,
            ConnectionString: t.ConnectionString,
            Modules: t.Modules);
    }
}
