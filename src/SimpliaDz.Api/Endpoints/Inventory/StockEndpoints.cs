using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/stock")
            .WithTags("Stock")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/variants/{variantId:guid}", async (Guid variantId,
            [FromServices] IStockService stock, CancellationToken ct) =>
            Results.Ok(await stock.GetByVariantAsync(variantId, ct)))
            .RequireAuthorization(PermissionNames.Stock.View);

        group.MapGet("/variants/{variantId:guid}/total", async (Guid variantId,
            [FromServices] IStockService stock, CancellationToken ct) =>
            Results.Ok(new { productVariantId = variantId,
                quantityOnHand = await stock.GetTotalByVariantAsync(variantId, ct) }))
            .RequireAuthorization(PermissionNames.Stock.View);

        group.MapGet("/locations/{locationId:guid}", async (Guid locationId,
            [FromServices] IStockService stock, CancellationToken ct) =>
            Results.Ok(await stock.GetByLocationAsync(locationId, ct)))
            .RequireAuthorization(PermissionNames.Stock.View);

        group.MapPost("/receipts", async ([FromBody] ReceiveStockRequest request,
            [FromServices] IStockService stock, CancellationToken ct) =>
        {
            var errors = ValidateIds(request.ProductVariantId, request.LocationId);
            if (request.Quantity <= 0) errors["quantity"] = ["Quantity must be greater than zero."];
            if (request.Reference?.Length > 50) errors["reference"] = ["Reference cannot exceed 50 characters."];
            if (request.Note?.Length > 500) errors["note"] = ["Note cannot exceed 500 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            await stock.ReceiveAsync(request.ProductVariantId, request.LocationId, request.Quantity,
                request.Reference, request.Note, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/adjustments", async ([FromBody] AdjustStockRequest request,
            [FromServices] IStockService stock, CancellationToken ct) =>
        {
            var errors = ValidateIds(request.ProductVariantId, request.LocationId);
            if (request.QuantityOnHand < 0) errors["quantityOnHand"] = ["QuantityOnHand cannot be negative."];
            if (request.Note?.Length > 500) errors["note"] = ["Note cannot exceed 500 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            await stock.AdjustToAsync(request.ProductVariantId, request.LocationId,
                request.QuantityOnHand, request.Note, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        return app;
    }

    private static Dictionary<string, string[]> ValidateIds(Guid productVariantId, Guid locationId)
    {
        var errors = new Dictionary<string, string[]>();
        if (productVariantId == Guid.Empty) errors["productVariantId"] = ["ProductVariantId is required."];
        if (locationId == Guid.Empty) errors["locationId"] = ["LocationId is required."];
        return errors;
    }
}

public sealed record ReceiveStockRequest(Guid ProductVariantId, Guid LocationId, int Quantity,
    string? Reference, string? Note);
public sealed record AdjustStockRequest(Guid ProductVariantId, Guid LocationId, int QuantityOnHand,
    string? Note);
