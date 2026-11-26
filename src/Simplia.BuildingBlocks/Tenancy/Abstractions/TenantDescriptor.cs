namespace Simplia.BuildingBlocks.Tenancy.Abstractions;

public sealed record TenantDescriptor(
    string TenantId,
    string Name,
    string ConnectionString,
    IReadOnlyDictionary<string, bool> Modules);
