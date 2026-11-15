using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.Audit.UseCases;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Sales;

public static class CashSessionEndpoints
{
    public static IEndpointRouteBuilder MapCashSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cash-sessions")
            .WithTags("Cash Sessions")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"));

        group.MapGet("/", async (Guid? locationId, int? page, int? pageSize,
            [FromServices] ICashSessionService sessions, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize || locationId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["filter"] = ["Location and pagination must be valid."] });
            return Results.Ok(await sessions.ListAsync(locationId,
                selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.CashSessions.View);

        group.MapGet("/current", async (Guid locationId,
            [FromServices] ICashSessionService sessions, CancellationToken ct) =>
        {
            if (locationId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["locationId"] = ["Location is required."] });
            return Results.Ok(await sessions.GetCurrentAsync(locationId, ct));
        }).RequireAuthorization(PermissionNames.CashSessions.View);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] ICashSessionService sessions, CancellationToken ct) =>
            Results.Ok(await sessions.GetAsync(id, ct)))
            .RequireAuthorization(PermissionNames.CashSessions.View);

        group.MapPost("/open", async ([FromBody] OpenCashSessionRequest request,
            ClaimsPrincipal principal, HttpContext http,
            [FromServices] ICashSessionService sessions,
            [FromServices] IAuditTrail audit, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.CashRegisterId == Guid.Empty)
                errors["cashRegisterId"] = ["Register is required."];
            if (!ValidMoney(request.OpeningCash))
                errors["openingCash"] = ["Opening cash must be a nonnegative amount with at most four decimal places."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await sessions.OpenAsync(request.CashRegisterId,
                request.OpeningCash, Actor(principal), ct);
            await BusinessAudit.RecordAsync(audit, http, "CashSessionOpened",
                "CashSession", result.Id, null,
                new Dictionary<string, object?> { ["cashRegisterId"] = result.CashRegisterId,
                    ["locationId"] = result.LocationId, ["openingCash"] = result.OpeningCash }, ct);
            return Results.Created($"/cash-sessions/{result.Id}", result);
        }).RequireAudit().RequireAuthorization(PermissionNames.CashSessions.Manage);

        group.MapPost("/{id:guid}/close", async (Guid id,
            [FromBody] CloseCashSessionRequest request, ClaimsPrincipal principal,
            HttpContext http, [FromServices] ICashSessionService sessions,
            [FromServices] IAuditTrail audit, CancellationToken ct) =>
        {
            if (!ValidMoney(request.CountedCash))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["countedCash"] = ["Counted cash must be a nonnegative amount with at most four decimal places."] });
            var result = await sessions.CloseAsync(id, request.CountedCash,
                Actor(principal), ct);
            await BusinessAudit.RecordAsync(audit, http, "CashSessionClosed",
                "CashSession", result.Id,
                new Dictionary<string, object?> { ["status"] = "Open" },
                new Dictionary<string, object?> { ["status"] = result.Status,
                    ["expectedCash"] = result.ExpectedCash,
                    ["countedCash"] = result.CountedCash,
                    ["difference"] = result.Difference }, ct);
            return Results.Ok(result);
        }).RequireAudit().RequireAuthorization(PermissionNames.CashSessions.Manage);

        group.MapPost("/{id:guid}/movements", async (Guid id,
            [FromBody] AddCashMovementRequest request, ClaimsPrincipal principal,
            HttpContext http, [FromServices] ICashSessionService sessions,
            [FromServices] IAuditTrail audit, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.AmountDelta == 0 || decimal.Round(request.AmountDelta, 4) != request.AmountDelta)
                errors["amountDelta"] = ["Amount must be nonzero with at most four decimal places."];
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
                errors["reason"] = ["Reason is required and cannot exceed 500 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await sessions.AddMovementAsync(id, request.AmountDelta,
                request.Reason, Actor(principal), ct);
            await BusinessAudit.RecordAsync(audit, http, "CashMoved",
                "CashSession", id, null,
                new Dictionary<string, object?> { ["amountDelta"] = result.AmountDelta,
                    ["reason"] = result.Reason }, ct);
            return Results.Created($"/cash-sessions/{id}/movements", result);
        }).RequireAudit().RequireAuthorization(PermissionNames.CashSessions.Manage);

        group.MapGet("/{id:guid}/movements", async (Guid id,
            [FromServices] ICashSessionService sessions, CancellationToken ct) =>
            Results.Ok(await sessions.GetMovementsAsync(id, ct)))
            .RequireAuthorization(PermissionNames.CashSessions.View);

        return app;
    }

    private static Guid Actor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
        id != Guid.Empty ? id : throw new UnauthorizedAccessException("Authenticated user is required.");

    private static bool ValidMoney(decimal amount) =>
        amount >= 0 && amount <= 99999999999999.9999m && decimal.Round(amount, 4) == amount;
}

public sealed record OpenCashSessionRequest(Guid CashRegisterId, decimal OpeningCash);
public sealed record CloseCashSessionRequest(decimal CountedCash);
public sealed record AddCashMovementRequest(decimal AmountDelta, string Reason);
