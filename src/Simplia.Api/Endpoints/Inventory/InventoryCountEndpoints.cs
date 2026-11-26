using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Inventory.UseCases.Services;
using Simplia.Audit.UseCases;
using Simplia.Api.Modules;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Inventory;

public static class InventoryCountEndpoints
{
    public static IEndpointRouteBuilder MapInventoryCountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/inventory-counts")
            .WithTags("Inventory Counts")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async (Guid? locationId, int? page, int? pageSize,
            [FromServices] IInventoryCountService counts, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (locationId == Guid.Empty) errors["locationId"] = ["Location ID must be valid."];
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await counts.ListAsync(locationId, selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Stock.View);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] IInventoryCountService counts, CancellationToken ct) =>
            Results.Ok(await counts.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.Stock.View);

        group.MapPost("/", async ([FromBody] StartInventoryCountRequest request,
            ClaimsPrincipal principal, [FromServices] IInventoryCountService counts,
            CancellationToken ct) =>
        {
            if (request.LocationId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["locationId"] = ["Location is required."] });
            var count = await counts.StartAsync(request.LocationId, RequireActor(principal), ct);
            return Results.Created($"/inventory-counts/{count.Id}", count);
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/{id:guid}/lines", async (Guid id,
            [FromBody] AddInventoryCountLineRequest request,
            [FromServices] IInventoryCountService counts, CancellationToken ct) =>
        {
            if (request.ProductVariantId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["productVariantId"] = ["Product variant is required."] });
            return Results.Ok(await counts.AddVariantAsync(id, request.ProductVariantId, ct));
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPut("/{id:guid}/lines/{productVariantId:guid}/count", async
            (Guid id, Guid productVariantId, [FromBody] SetCountedQuantityRequest request,
            [FromServices] IInventoryCountService counts, CancellationToken ct) =>
        {
            if (request.CountedQuantity < 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["countedQuantity"] = ["Physical quantity cannot be negative."] });
            return Results.Ok(await counts.SetCountedQuantityAsync(id, productVariantId,
                request.CountedQuantity, ct));
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/{id:guid}/complete", async (Guid id, ClaimsPrincipal principal,
            HttpContext http, [FromServices] IInventoryCountService counts,
            [FromServices] IAuditTrail audit, CancellationToken ct) =>
        {
            var result = await counts.CompleteAsync(id, RequireActor(principal), ct);
            await BusinessAudit.RecordAsync(audit, http, "InventoryCountCompleted",
                "InventoryCount", id,
                new Dictionary<string, object?> { ["status"] = "Draft",
                    ["locationId"] = result.LocationId },
                new Dictionary<string, object?> { ["status"] = result.Status,
                    ["lineCount"] = result.Lines.Count,
                    ["adjustedLineCount"] = result.Lines.Count(x => x.Difference != 0) }, ct);
            return Results.Ok(result);
        })
            .RequireAudit()
            .RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/{id:guid}/cancel", async (Guid id,
            [FromServices] IInventoryCountService counts, CancellationToken ct) =>
        {
            await counts.CancelAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        return app;
    }

    private static string RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
        userId != Guid.Empty
            ? userId.ToString()
            : throw new UnauthorizedAccessException("Authenticated user ID is required for inventory counts.");
}

public sealed record StartInventoryCountRequest(Guid LocationId);
public sealed record AddInventoryCountLineRequest(Guid ProductVariantId);
public sealed record SetCountedQuantityRequest(int CountedQuantity);
