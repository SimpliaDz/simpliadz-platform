using Simplia.BuildingBlocks.Tenancy.Abstractions;

namespace Simplia.BuildingBlocks.Tenancy.Resolution;

public sealed class CliTenantResolver : ITenantResolver
{
    private readonly string[] _args;

    public CliTenantResolver(string[] args) => _args = args;

    public string ResolveTenantId()
    {
        var idx = Array.FindIndex(_args, a => string.Equals(a, "--tenant", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && idx + 1 < _args.Length && !string.IsNullOrWhiteSpace(_args[idx + 1]))
            return _args[idx + 1].Trim();

        throw new ArgumentException("Missing required argument: --tenant <tenantId>");
    }
}
