using Microsoft.AspNetCore.Mvc;
using SimpliaDz.Audit.UseCases;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Audit;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/audit/entries", async (Guid? userId, string? action,
            string? entityType, Guid? entityId, DateTimeOffset? from,
            DateTimeOffset? to, int? page, int? pageSize,
            [FromServices] IAuditTrail audit, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (userId == Guid.Empty || entityId == Guid.Empty)
                errors["id"] = ["Filter IDs must be valid."];
            if (from > to) errors["dateRange"] = ["From must be before to."];
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await audit.QueryAsync(userId, action, entityType,
                entityId, from, to, selectedPage, selectedSize, ct));
        })
        .WithTags("Audit")
        .WithMetadata(new TenantRequiredMetadata())
        .WithMetadata(new ModuleEndpointMetadata("Audit"))
        .RequireAuthorization(PermissionNames.Audit.View);
        return app;
    }
}
