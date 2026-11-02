using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Sales;

public static class SaleEndpoints
{
    public static IEndpointRouteBuilder MapSaleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sales")
            .WithTags("Sales")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"));

        group.MapGet("/", async (Guid? locationId, Guid? customerId, string? status,
            int? page, int? pageSize, [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (locationId == Guid.Empty) errors["locationId"] = ["Location ID must be valid."];
            if (customerId == Guid.Empty) errors["customerId"] = ["Customer ID must be valid."];
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (!string.IsNullOrWhiteSpace(status) &&
                (!Enum.TryParse<SaleStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed)))
                errors["status"] = ["Invalid sale status."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await sales.ListAsync(locationId, customerId, status,
                selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Sales.View);

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] ISaleService sales,
            CancellationToken ct) => Results.Ok(await sales.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.Sales.View);

        group.MapPost("/", async ([FromBody] CreateSaleRequest request,
            ClaimsPrincipal principal, [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.LocationId == Guid.Empty) errors["locationId"] = ["Location is required."];
            if (request.CustomerId == Guid.Empty) errors["customerId"] = ["Customer ID must be valid."];
            if (request.PriceListId == Guid.Empty) errors["priceListId"] = ["Price list is required."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var sale = await sales.CreateDraftAsync(request.LocationId, request.CustomerId,
                RequireActor(principal), request.PriceListId, ct);
            return Results.Created($"/sales/{sale.Id}", sale);
        }).RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapPost("/{id:guid}/lines", async (Guid id, [FromBody] AddSaleLineRequest request,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Sku) == string.IsNullOrWhiteSpace(request.Barcode))
                errors["variant"] = ["Provide exactly one SKU or barcode."];
            if (request.Sku?.Trim().Length > 64) errors["sku"] = ["SKU cannot exceed 64 characters."];
            if (request.Barcode?.Trim().Length > 64)
                errors["barcode"] = ["Barcode cannot exceed 64 characters."];
            if (request.Quantity <= 0) errors["quantity"] = ["Quantity must be greater than zero."];
            if (request.DiscountAmount < 0 || request.DiscountAmount > 99999999999999.9999m ||
                decimal.Round(request.DiscountAmount, 4) != request.DiscountAmount)
                errors["discountAmount"] = ["Discount must be nonnegative and fit decimal(18,4)."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await sales.AddLineAsync(id, request.Sku, request.Barcode,
                request.Quantity, request.DiscountAmount, ct));
        }).RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapPut("/{id:guid}/discount", async (Guid id,
            [FromBody] SetSaleDiscountRequest request, [FromServices] ISaleService sales,
            CancellationToken ct) =>
        {
            if (request.DiscountAmount < 0 || request.DiscountAmount > 99999999999999.9999m ||
                decimal.Round(request.DiscountAmount, 4) != request.DiscountAmount)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["discountAmount"] = ["Discount must be nonnegative and fit decimal(18,4)."] });
            return Results.Ok(await sales.SetDiscountAsync(id, request.DiscountAmount, ct));
        }).RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapPost("/{id:guid}/complete", async (Guid id, ClaimsPrincipal principal,
            [FromServices] ISaleService sales, CancellationToken ct) =>
            Results.Ok(await sales.CompleteAsync(id, RequireActor(principal), ct)))
            .RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapPost("/{id:guid}/cancel", async (Guid id,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            await sales.CancelAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Sales.Manage);

        return app;
    }

    private static Guid RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
        userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("Authenticated user ID is required for sales.");
}

public sealed record CreateSaleRequest(Guid LocationId, Guid? CustomerId, Guid PriceListId);
public sealed record AddSaleLineRequest(string? Sku, string? Barcode, int Quantity,
    decimal DiscountAmount);
public sealed record SetSaleDiscountRequest(decimal DiscountAmount);
