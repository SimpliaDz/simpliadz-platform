using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class StockTransferEndpoints
{
    public static IEndpointRouteBuilder MapStockTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/stock-transfers")
            .WithTags("Stock Transfers")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async (int? page, int? pageSize,
            [FromServices] IStockTransferService transfers, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."]
                });
            return Results.Ok(await transfers.ListAsync(selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Stock.View);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] IStockTransferService transfers, CancellationToken ct) =>
            Results.Ok(await transfers.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.Stock.View);

        group.MapPost("/", async ([FromBody] CreateStockTransferRequest request,
            ClaimsPrincipal principal, [FromServices] IStockTransferService transfers,
            CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.SourceLocationId == Guid.Empty)
                errors["sourceLocationId"] = ["Source location is required."];
            if (request.DestinationLocationId == Guid.Empty)
                errors["destinationLocationId"] = ["Destination location is required."];
            if (request.SourceLocationId != Guid.Empty &&
                request.SourceLocationId == request.DestinationLocationId)
                errors["destinationLocationId"] = ["Source and destination must differ."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var transfer = await transfers.CreateDraftAsync(request.SourceLocationId,
                request.DestinationLocationId, RequireActor(principal), ct);
            return Results.Created($"/stock-transfers/{transfer.Id}", transfer);
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/{id:guid}/lines", async (Guid id,
            [FromBody] AddStockTransferLineRequest request,
            [FromServices] IStockTransferService transfers, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.ProductVariantId == Guid.Empty)
                errors["productVariantId"] = ["Product variant is required."];
            if (request.Quantity <= 0)
                errors["quantity"] = ["Quantity must be greater than zero."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            return Results.Ok(await transfers.AddLineAsync(id, request.ProductVariantId,
                request.Quantity, ct));
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/{id:guid}/complete", async (Guid id, ClaimsPrincipal principal,
            [FromServices] IStockTransferService transfers, CancellationToken ct) =>
            Results.Ok(await transfers.CompleteAsync(id, RequireActor(principal), ct)))
            .RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/{id:guid}/cancel", async (Guid id,
            [FromServices] IStockTransferService transfers, CancellationToken ct) =>
        {
            await transfers.CancelAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        return app;
    }

    private static string RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
        userId != Guid.Empty
            ? userId.ToString()
            : throw new UnauthorizedAccessException("Authenticated user ID is required for stock transfers.");
}

public sealed record CreateStockTransferRequest(Guid SourceLocationId, Guid DestinationLocationId);
public sealed record AddStockTransferLineRequest(Guid ProductVariantId, int Quantity);
