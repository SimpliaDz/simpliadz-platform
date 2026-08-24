using Microsoft.Extensions.Primitives;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;

namespace SimpliaDz.Api.Tenancy;

/// <summary>
/// Resolves the current tenant from the required X-Tenant-Id request header.
/// </summary>
public sealed class HttpTenantResolver : ITenantResolver
{
    public const string TenantHeaderName = "X-Tenant-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantResolver(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public string ResolveTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Tenant resolution requires an active HTTP request.");

        if (!httpContext.Request.Headers.TryGetValue(TenantHeaderName, out StringValues values) ||
            values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]))
        {
            throw new InvalidOperationException(
                $"Request must include exactly one '{TenantHeaderName}' header.");
        }

        return values[0]!.Trim();
    }
}
