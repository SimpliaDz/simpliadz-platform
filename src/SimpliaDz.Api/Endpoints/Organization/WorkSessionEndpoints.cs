using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.Audit.UseCases;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Organization;

public static class WorkSessionEndpoints
{
    public static IEndpointRouteBuilder MapWorkSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/work-sessions")
            .WithTags("Worked Hours")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Organization"));

        group.MapPost("/clock-in", async (ClaimsPrincipal principal,
            HttpContext http, [FromServices] WorkSessionService work, CancellationToken ct) =>
        {
            var session = await work.ClockInAsync(Actor(principal), ct);
            await BusinessAudit.RecordIfEnabledAsync(http, "WorkSessionClockedIn",
                "WorkSession", session.Id, null, ct);
            return Results.Created($"/work-sessions/{session.Id}", session);
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization();

        group.MapPost("/{id:guid}/clock-out", async (Guid id, ClaimsPrincipal principal,
            HttpContext http, [FromServices] WorkSessionService work, CancellationToken ct) =>
        {
            var session = await work.ClockOutAsync(id, Actor(principal), ct);
            await BusinessAudit.RecordIfEnabledAsync(http, "WorkSessionClockedOut",
                "WorkSession", session.Id, null, ct);
            return Results.Ok(session);
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal principal, DateOnly? fromDate,
            DateOnly? toDate, int? page, int? pageSize,
            [FromServices] WorkSessionService work, CancellationToken ct) =>
            Results.Ok(await work.ListAsync(Actor(principal), fromDate, toDate,
                page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization();

        group.MapGet("/", async (Guid? userId, DateOnly? fromDate,
            DateOnly? toDate, int? page, int? pageSize,
            [FromServices] WorkSessionService work, CancellationToken ct) =>
            Results.Ok(await work.ListAsync(userId, fromDate, toDate,
                page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(PermissionNames.WorkSessions.View);

        group.MapPut("/{id:guid}", async (Guid id,
            [FromBody] CorrectWorkSessionRequest request, ClaimsPrincipal principal,
            HttpContext http, [FromServices] WorkSessionService work,
            [FromServices] IAuditTrail audit, CancellationToken ct) =>
        {
            var updated = await work.CorrectAsync(id, request.ClockInAt,
                request.ClockOutAt, request.Reason, Actor(principal), ct);
            await BusinessAudit.RecordAsync(audit, http, "WorkSessionCorrected",
                "WorkSession", id, null,
                new Dictionary<string, object?>
                {
                    ["clockInAt"] = updated.ClockInAt.ToString("O"),
                    ["clockOutAt"] = updated.ClockOutAt?.ToString("O"),
                    ["reason"] = request.Reason
                }, ct);
            return Results.Ok(updated);
        }).RequireAudit().RequireAuthorization(PermissionNames.WorkSessions.Manage);

        group.MapGet("/{id:guid}/corrections", async (Guid id,
            [FromServices] WorkSessionService work, CancellationToken ct) =>
            Results.Ok(await work.CorrectionsAsync(id, ct)))
            .RequireAuthorization(PermissionNames.WorkSessions.View);

        app.MapGet("/reports/work-hours", async (Guid? userId, DateOnly fromDate,
            DateOnly toDate, [FromServices] WorkSessionService work,
            CancellationToken ct) =>
            Results.Ok(await work.ReportAsync(userId, fromDate, toDate, ct)))
            .WithTags("Worked Hours")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Organization"))
            .RequireAuthorization(PermissionNames.WorkSessions.Report);

        return app;
    }

    private static Guid Actor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
        id != Guid.Empty ? id : throw new UnauthorizedAccessException("Authenticated user is required.");
}

public sealed record CorrectWorkSessionRequest(DateTimeOffset ClockInAt,
    DateTimeOffset? ClockOutAt, string Reason);
