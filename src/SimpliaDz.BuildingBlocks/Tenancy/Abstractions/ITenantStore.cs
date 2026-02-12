namespace SimpliaDz.BuildingBlocks.Tenancy.Abstractions;

public interface ITenantStore
{
    TenantDescriptor GetTenant(string tenantId);
}
