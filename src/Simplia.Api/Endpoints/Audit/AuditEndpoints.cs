using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simplia.Identity.Infrastructure.Data;
using Simplia.Audit.UseCases;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Audit;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/audit/entries", async (Guid? userId, string? action,
            string? entityType, Guid? entityId, DateTimeOffset? from,
            DateTimeOffset? to, int? page, int? pageSize,
            [FromServices] IAuditTrail audit, [FromServices] IdentityDbContext identity, CancellationToken ct) =>
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
            var history = await audit.QueryAsync(userId, action, entityType,
                entityId, from, to, selectedPage, selectedSize, ct);
            if (history.Items.Count == 0) return Results.Ok(history);

            var actorIds = history.Items.Select(entry => entry.UserId).Distinct().ToArray();
            var actors = await identity.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id))
                .Select(user => new { user.Id, user.FirstName, user.LastName, user.UserName })
                .ToDictionaryAsync(user => user.Id, ct);
            return Results.Ok(history with
            {
                Items = history.Items.Select(entry =>
                {
                    if (!actors.TryGetValue(entry.UserId, out var actor)) return entry;
                    var name = $"{actor.FirstName} {actor.LastName}".Trim();
                    return entry with { ActorName = name.Length > 0 ? name : actor.UserName };
                }).ToArray()
            });
        })
        .WithTags("Audit")
        .WithMetadata(new TenantRequiredMetadata())
        .WithMetadata(new ModuleEndpointMetadata("Audit"))
        .RequireAuthorization(PermissionNames.Audit.View);
        return app;
    }
}
