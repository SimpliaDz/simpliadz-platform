namespace Simplia.BuildingBlocks.Tenancy.Abstractions;

public interface ITenantStore
{
    TenantDescriptor GetTenant(string tenantId);
}
