using System.Security.Claims;
using SimpliaDz.Audit.UseCases;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;

namespace SimpliaDz.Api.Modules;

public static class BusinessAudit
{
    public static RouteHandlerBuilder RequireAudit(this RouteHandlerBuilder route) =>
        route.AddEndpointFilter(async (context, next) =>
        {
            var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            if (!tenant.IsModuleEnabled("Audit"))
                return Results.Conflict(new { message = "Audit must be enabled for this action." });
            return await next(context);
        });

    public static Task RecordAsync(IAuditTrail audit, HttpContext context,
        string action, string entityType, Guid entityId,
        IReadOnlyDictionary<string, object?>? oldValues,
        IReadOnlyDictionary<string, object?>? newValues, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            userId == Guid.Empty)
            throw new UnauthorizedAccessException("Authenticated user ID is required for audited actions.");
        return audit.AppendAsync(userId, action, entityType, entityId,
            oldValues, newValues, context.TraceIdentifier, ct);
    }
}
