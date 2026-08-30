namespace SimpliaDz.BuildingBlocks.Tenancy.Configuration;

public sealed class TenancyOptions
{
    public string ActiveTenantId { get; init; } = "";

    public Dictionary<string, TenantOptions> Tenants { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TenantOptions
{
    public string Name { get; init; } = "";
    public string ConnectionString { get; init; } = "";
    public Dictionary<string, bool> Modules { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
