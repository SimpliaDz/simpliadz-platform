using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Inventory.UseCases.Services;
using Simplia.Audit.UseCases;
using Simplia.Api.Modules;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Inventory;

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

        group.MapGet("/overview", async ([FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var balances = await db.StockBalances.AsNoTracking().Select(x => new
            {
                ProductVariantId = x.VariantId, x.LocationId, x.QuantityOnHand, x.UpdatedAt
            }).ToListAsync(ct);
            var prices = await (from item in db.PriceListItems.AsNoTracking()
                join list in db.PriceLists.AsNoTracking() on item.PriceListId equals list.Id
                where list.IsActive
                orderby list.Name
                select new { item.ProductVariantId, PriceListName = list.Name, list.CurrencyCode, item.Price })
                .ToListAsync(ct);
            var attributes = await db.ProductVariants.AsNoTracking().Select(variant => new
            {
                ProductVariantId = variant.Id,
                Values = variant.AttributeValues.Select(value => value.AttributeValue.Value)
                    .OrderBy(value => value).ToList(),
            }).ToListAsync(ct);
            return Results.Ok(new { balances, prices, attributes });
        }).RequireAuthorization(PermissionNames.Stock.View);

        group.MapGet("/movements", async (Guid? productVariantId, Guid? locationId,
            int? page, int? pageSize, [FromServices] IStockService stock, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."]
                });

            return Results.Ok(await stock.GetMovementsAsync(productVariantId, locationId,
                selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Stock.View);

        group.MapPost("/opening", async ([FromBody] OpenStockRequest request,
            ClaimsPrincipal principal, [FromServices] IStockService stock, CancellationToken ct) =>
        {
            var errors = ValidateIds(request.ProductVariantId, request.LocationId);
            if (request.Quantity < 0) errors["quantity"] = ["Quantity cannot be negative."];
            if (request.Reason?.Length > 500) errors["reason"] = ["Reason cannot exceed 500 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var result = await stock.OpenAsync(request.ProductVariantId, request.LocationId,
                request.Quantity, request.Reason, RequireActor(principal), ct);
            return Results.Created($"/stock/variants/{result.ProductVariantId}", result);
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/receipts", async ([FromBody] ReceiveStockRequest request,
            ClaimsPrincipal principal, [FromServices] IStockService stock, CancellationToken ct) =>
        {
            var errors = ValidateIds(request.ProductVariantId, request.LocationId);
            if (request.Quantity <= 0) errors["quantity"] = ["Quantity must be greater than zero."];
            if (request.Reference?.Length > 50) errors["reference"] = ["Reference cannot exceed 50 characters."];
            if ((request.Reason ?? request.Note)?.Length > 500) errors["reason"] = ["Reason cannot exceed 500 characters."];
            if (request.ReferenceType?.Length > 100) errors["referenceType"] = ["ReferenceType cannot exceed 100 characters."];
            if (request.ReferenceId.HasValue != !string.IsNullOrWhiteSpace(request.ReferenceType) ||
                request.ReferenceId == Guid.Empty)
                errors["referenceType"] = ["ReferenceType and ReferenceId must be provided together."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            await stock.ReceiveAsync(request.ProductVariantId, request.LocationId, request.Quantity,
                request.Reference, request.Reason ?? request.Note, request.ReferenceType,
                request.ReferenceId, RequireActor(principal), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        group.MapPost("/adjustments", async ([FromBody] AdjustStockRequest request,
            ClaimsPrincipal principal, HttpContext http,
            [FromServices] IStockService stock, [FromServices] IAuditTrail audit,
            CancellationToken ct) =>
        {
            var errors = ValidateIds(request.ProductVariantId, request.LocationId);
            if (request.QuantityDelta.HasValue == request.QuantityOnHand.HasValue)
                errors["quantityDelta"] = ["Provide exactly one of quantityDelta or quantityOnHand."];
            if (request.QuantityDelta == 0)
                errors["quantityDelta"] = ["QuantityDelta cannot be zero."];
            if (request.QuantityOnHand < 0)
                errors["quantityOnHand"] = ["QuantityOnHand cannot be negative."];
            var reason = request.Reason ?? request.Note;
            if (string.IsNullOrWhiteSpace(reason)) errors["reason"] = ["Reason is required for an adjustment."];
            else if (reason.Length > 500) errors["reason"] = ["Reason cannot exceed 500 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var actor = RequireActor(principal);
            var before = (await stock.GetByVariantAsync(request.ProductVariantId, ct))
                .FirstOrDefault(x => x.LocationId == request.LocationId)?.QuantityOnHand ?? 0;
            if (request.QuantityDelta.HasValue)
                await stock.AdjustByAsync(request.ProductVariantId, request.LocationId,
                    request.QuantityDelta.Value, reason!, actor, ct);
            else
                await stock.AdjustToAsync(request.ProductVariantId, request.LocationId,
                    request.QuantityOnHand!.Value, reason!, actor, ct);
            var after = (await stock.GetByVariantAsync(request.ProductVariantId, ct))
                .FirstOrDefault(x => x.LocationId == request.LocationId)?.QuantityOnHand ?? 0;
            if (after != before)
                await BusinessAudit.RecordAsync(audit, http, "StockAdjusted", "ProductVariant",
                    request.ProductVariantId,
                    new Dictionary<string, object?> { ["locationId"] = request.LocationId,
                        ["quantityOnHand"] = before },
                    new Dictionary<string, object?> { ["locationId"] = request.LocationId,
                        ["quantityOnHand"] = after, ["reason"] = reason }, ct);
            return Results.NoContent();
        }).RequireAudit().RequireAuthorization(PermissionNames.Stock.Manage);

        return app;
    }

    private static Dictionary<string, string[]> ValidateIds(Guid productVariantId, Guid locationId)
    {
        var errors = new Dictionary<string, string[]>();
        if (productVariantId == Guid.Empty) errors["productVariantId"] = ["ProductVariantId is required."];
        if (locationId == Guid.Empty) errors["locationId"] = ["LocationId is required."];
        return errors;
    }

    private static string RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
        userId != Guid.Empty
            ? userId.ToString()
            : throw new UnauthorizedAccessException("Authenticated user ID is required for stock changes.");
}

public sealed record ReceiveStockRequest(Guid ProductVariantId, Guid LocationId, int Quantity,
    string? Reference, string? Note, string? ReferenceType = null, Guid? ReferenceId = null,
    string? Reason = null);
public sealed record OpenStockRequest(Guid ProductVariantId, Guid LocationId, int Quantity,
    string? Reason);
public sealed record AdjustStockRequest(Guid ProductVariantId, Guid LocationId,
    int? QuantityOnHand = null, string? Note = null, string? Reason = null,
    int? QuantityDelta = null);
