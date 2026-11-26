using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Sales.UseCases.Services;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Sales;

public static class PosProductLookupEndpoints
{
    public static IEndpointRouteBuilder MapPosProductLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/pos/products")
            .WithTags("POS Product Lookup")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"));

        group.MapGet("/by-barcode/{barcode}", async (string barcode, Guid priceListId,
            Guid? locationId, [FromServices] IPosProductLookupService lookup, CancellationToken ct) =>
        {
            var errors = Validate(priceListId, locationId, barcode);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await lookup.FindByBarcodeAsync(barcode, priceListId, locationId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapGet("/by-sku/{sku}", async (string sku, Guid priceListId,
            Guid? locationId, [FromServices] IPosProductLookupService lookup, CancellationToken ct) =>
        {
            var errors = Validate(priceListId, locationId, sku);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await lookup.FindBySkuAsync(sku, priceListId, locationId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapGet("/", async (string? search, Guid priceListId, Guid? locationId,
            int? page, int? pageSize, [FromServices] IPosProductLookupService lookup,
            CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 25;
            var errors = Validate(priceListId, locationId);
            if (string.IsNullOrWhiteSpace(search) || search.Trim().Length > 200)
                errors["search"] = ["Product name prefix must contain 1 to 200 characters."];
            if (selectedPage < 1 || selectedSize is < 1 or > 50 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 50."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await lookup.SearchByProductNameAsync(search!, priceListId,
                locationId, selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Sales.Manage);

        return app;
    }

    private static Dictionary<string, string[]> Validate(Guid priceListId, Guid? locationId,
        string? exactCode = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (priceListId == Guid.Empty) errors["priceListId"] = ["Price list is required."];
        if (locationId == Guid.Empty) errors["locationId"] = ["Location ID must be valid."];
        if (exactCode is not null && (string.IsNullOrWhiteSpace(exactCode) ||
            exactCode.Trim().Length > 64))
            errors["code"] = ["SKU or barcode must contain 1 to 64 characters."];
        return errors;
    }
}
