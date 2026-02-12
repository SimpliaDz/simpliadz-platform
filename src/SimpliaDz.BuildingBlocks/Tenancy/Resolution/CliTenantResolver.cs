using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;

namespace SimpliaDz.BuildingBlocks.Tenancy.Resolution;

public sealed class CliTenantResolver : ITenantResolver
{
    private readonly string[] _args;

    public CliTenantResolver(string[] args) => _args = args;

    public string ResolveTenantId()
    {
        var idx = Array.FindIndex(_args, a => string.Equals(a, "--tenant", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && idx + 1 < _args.Length) return _args[idx + 1];
        throw new ArgumentException("Missing required argument: --tenant <tenantId>");
    }
}
