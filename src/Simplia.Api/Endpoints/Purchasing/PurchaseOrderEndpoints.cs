using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Purchasing.UseCases.Services;
using Simplia.Purchasing.Core.Entities;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Purchasing;

public static class PurchaseOrderEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/purchase-orders")
            .WithTags("Purchase Orders")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Purchasing"));

        group.MapGet("/", async (Guid? supplierId, string? status, int? page, int? pageSize,
            [FromServices] IPurchaseOrderService orders, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (supplierId == Guid.Empty) errors["supplierId"] = ["Supplier ID must be valid."];
            if (!string.IsNullOrWhiteSpace(status) &&
                (!Enum.TryParse<PurchaseOrderStatus>(status, true, out var parsed) ||
                 !Enum.IsDefined(parsed)))
                errors["status"] = ["Invalid purchase order status."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await orders.ListAsync(supplierId, status, selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.PurchaseOrders.View);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] IPurchaseOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.PurchaseOrders.View);

        group.MapPost("/", async ([FromBody] CreatePurchaseOrderRequest request,
            ClaimsPrincipal principal, [FromServices] IPurchaseOrderService orders, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.SupplierId == Guid.Empty) errors["supplierId"] = ["Supplier is required."];
            if (request.DestinationLocationId == Guid.Empty)
                errors["destinationLocationId"] = ["Destination location is required."];
            if (request.Notes?.Trim().Length > 2000)
                errors["notes"] = ["Notes cannot exceed 2000 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var order = await orders.CreateDraftAsync(request.SupplierId, request.DestinationLocationId,
                request.Notes, RequireActor(principal), ct);
            return Results.Created($"/purchase-orders/{order.Id}", order);
        }).RequireAuthorization(PermissionNames.PurchaseOrders.Manage);

        group.MapPost("/{id:guid}/lines", async (Guid id, [FromBody] AddPurchaseOrderLineRequest request,
            [FromServices] IPurchaseOrderService orders, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.ProductVariantId == Guid.Empty)
                errors["productVariantId"] = ["Product variant is required."];
            if (request.OrderedQuantity <= 0)
                errors["orderedQuantity"] = ["Ordered quantity must be greater than zero."];
            if (request.UnitCost < 0 || request.UnitCost > 99999999999999.9999m ||
                decimal.Round(request.UnitCost, 4) != request.UnitCost)
                errors["unitCost"] = ["Unit cost must be nonnegative and fit decimal(18,4)."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await orders.AddLineAsync(id, request.ProductVariantId,
                request.OrderedQuantity, request.UnitCost, ct));
        }).RequireAuthorization(PermissionNames.PurchaseOrders.Manage);

        group.MapPut("/{id:guid}/notes", async (Guid id, [FromBody] UpdatePurchaseOrderNotesRequest request,
            [FromServices] IPurchaseOrderService orders, CancellationToken ct) =>
        {
            if (request.Notes?.Trim().Length > 2000)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["notes"] = ["Notes cannot exceed 2000 characters."] });
            return Results.Ok(await orders.UpdateNotesAsync(id, request.Notes, ct));
        }).RequireAuthorization(PermissionNames.PurchaseOrders.Manage);

        group.MapPost("/{id:guid}/order", async (Guid id,
            [FromServices] IPurchaseOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.PlaceOrderAsync(id, ct)))
            .RequireAuthorization(PermissionNames.PurchaseOrders.Manage);

        group.MapPost("/{id:guid}/cancel", async (Guid id,
            [FromServices] IPurchaseOrderService orders, CancellationToken ct) =>
        {
            await orders.CancelAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.PurchaseOrders.Manage);

        return app;
    }

    private static string RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
        userId != Guid.Empty
            ? userId.ToString()
            : throw new UnauthorizedAccessException("Authenticated user ID is required for purchase orders.");
}

public sealed record CreatePurchaseOrderRequest(Guid SupplierId, Guid DestinationLocationId, string? Notes);
public sealed record AddPurchaseOrderLineRequest(Guid ProductVariantId, int OrderedQuantity, decimal UnitCost);
public sealed record UpdatePurchaseOrderNotesRequest(string? Notes);
