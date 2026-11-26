namespace Simplia.BuildingBlocks.Tenancy.Abstractions;

public interface ITenantContext
{
    string TenantId { get; }
    string Name { get; }
    string ConnectionString { get; }
    bool IsModuleEnabled(string moduleName);
}
