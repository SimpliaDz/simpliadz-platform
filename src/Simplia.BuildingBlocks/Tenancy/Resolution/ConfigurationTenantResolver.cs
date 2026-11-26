using Microsoft.Extensions.Options;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Configuration;

namespace Simplia.BuildingBlocks.Tenancy.Resolution;

/// <summary>
/// Resolves the single tenant assigned to this application instance.
/// </summary>
public sealed class ConfigurationTenantResolver : ITenantResolver
{
    private readonly string _tenantId;

    public ConfigurationTenantResolver(IOptions<TenancyOptions> options)
    {
        _tenantId = options.Value.ActiveTenantId?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(_tenantId))
        {
            throw new InvalidOperationException(
                "Tenancy:ActiveTenantId must identify the tenant assigned to this API instance.");
        }
    }

    public string ResolveTenantId() => _tenantId;
}
