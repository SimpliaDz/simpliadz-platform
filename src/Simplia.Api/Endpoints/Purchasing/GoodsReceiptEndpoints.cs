using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Purchasing.UseCases.Services;
using Simplia.Audit.UseCases;
using Simplia.Api.Modules;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Purchasing;

public static class GoodsReceiptEndpoints
{
    public static IEndpointRouteBuilder MapGoodsReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/goods-receipts")
            .WithTags("Goods Receipts")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Purchasing"));

        group.MapPost("/", async ([FromBody] ReceiveGoodsRequest request,
            ClaimsPrincipal principal, HttpContext http,
            [FromServices] IGoodsReceiptService receipts,
            [FromServices] IAuditTrail audit, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.Id == Guid.Empty) errors["id"] = ["A receipt ID is required for safe retries."];
            if (request.PurchaseOrderId == Guid.Empty)
                errors["purchaseOrderId"] = ["Purchase order is required."];
            if (request.Lines is not { Count: > 0 })
                errors["lines"] = ["At least one receipt line is required."];
            else
            {
                if (request.Lines.Any(x => x.PurchaseOrderLineId == Guid.Empty))
                    errors["lines.purchaseOrderLineId"] = ["Each line needs a purchase order line ID."];
                if (request.Lines.Any(x => x.QuantityReceived <= 0))
                    errors["lines.quantityReceived"] = ["Received quantity must be greater than zero."];
                if (request.Lines.Select(x => x.PurchaseOrderLineId).Distinct().Count() != request.Lines.Count)
                    errors["lines"] = ["Purchase order lines cannot be repeated in one receipt."];
            }
            if (request.DeliveryReference?.Trim().Length > 500)
                errors["deliveryReference"] = ["Delivery reference cannot exceed 500 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await receipts.ReceiveAsync(request.Id, request.PurchaseOrderId,
                request.Lines!, RequireActor(principal), ct, request.DeliveryReference);
            await BusinessAudit.RecordAsync(audit, http, "GoodsReceived", "GoodsReceipt",
                result.Id, null,
                new Dictionary<string, object?> { ["purchaseOrderId"] = result.PurchaseOrderId,
                    ["deliveryReference"] = result.DeliveryReference,
                    ["locationId"] = result.LocationId, ["lineCount"] = result.Lines.Count,
                    ["quantityReceived"] = result.Lines.Sum(x => x.QuantityReceived) }, ct);
            return Results.Created($"/goods-receipts/{result.Id}", result);
        }).RequireAudit().RequireAuthorization(PermissionNames.GoodsReceipts.Manage);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] IGoodsReceiptService receipts, CancellationToken ct) =>
            Results.Ok(await receipts.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.GoodsReceipts.View);

        group.MapGet("/by-purchase-order/{purchaseOrderId:guid}", async (Guid purchaseOrderId,
            [FromServices] IGoodsReceiptService receipts, CancellationToken ct) =>
            Results.Ok(await receipts.ListByPurchaseOrderAsync(purchaseOrderId, ct)))
            .RequireAuthorization(PermissionNames.GoodsReceipts.View);

        return app;
    }

    private static string RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
        userId != Guid.Empty
            ? userId.ToString()
            : throw new UnauthorizedAccessException("Authenticated user ID is required for goods receipts.");
}

public sealed record ReceiveGoodsRequest(Guid Id, Guid PurchaseOrderId,
    IReadOnlyList<ReceiveGoodsLine>? Lines, string? DeliveryReference = null);
