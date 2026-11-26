using Simplia.BuildingBlocks.Tenancy.Abstractions;

namespace Simplia.BuildingBlocks.Tenancy.Resolution;

public sealed class TenantContext : ITenantContext
{
    private readonly TenantDescriptor _tenant;

    public TenantContext(TenantDescriptor tenant) => _tenant = tenant;

    public string TenantId => _tenant.TenantId;
    public string Name => _tenant.Name;
    public string ConnectionString => _tenant.ConnectionString;

    public bool IsModuleEnabled(string moduleName)
        => _tenant.Modules.TryGetValue(moduleName, out var enabled) && enabled;
}
