using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Inventory.UseCases.Services;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Inventory;

public static class InventoryPolicyEndpoints
{
    public static IEndpointRouteBuilder MapInventoryPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/stock/out-of-stock", async (Guid locationId, int? page,
            int? pageSize, [FromServices] IInventoryPolicyService policies,
            CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (locationId == Guid.Empty)
                errors["locationId"] = ["Location is required."];
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await policies.GetOutOfStockByLocationAsync(locationId,
                selectedPage, selectedSize, ct));
        }).WithTags("Inventory Policies")
          .WithMetadata(new TenantRequiredMetadata())
          .WithMetadata(new ModuleEndpointMetadata("Inventory"))
          .RequireAuthorization(PermissionNames.Stock.View);

        var group = app.MapGroup("/stock/policies")
            .WithTags("Inventory Policies")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async (Guid? locationId, bool? belowReorderPoint,
            bool? outOfStock, int? page, int? pageSize,
            [FromServices] IInventoryPolicyService policies, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (locationId == Guid.Empty)
                errors["locationId"] = ["Location ID must be valid."];
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await policies.ListAsync(locationId, belowReorderPoint,
                outOfStock, selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Stock.View);

        group.MapGet("/{productVariantId:guid}/{locationId:guid}", async
            (Guid productVariantId, Guid locationId,
            [FromServices] IInventoryPolicyService policies, CancellationToken ct) =>
            Results.Ok(await policies.GetAsync(productVariantId, locationId, ct)))
            .RequireAuthorization(PermissionNames.Stock.View);

        group.MapPut("/{productVariantId:guid}/{locationId:guid}", async
            (Guid productVariantId, Guid locationId,
            [FromBody] ConfigureInventoryPolicyRequest request,
            [FromServices] IInventoryPolicyService policies, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (productVariantId == Guid.Empty)
                errors["productVariantId"] = ["Product variant is required."];
            if (locationId == Guid.Empty)
                errors["locationId"] = ["Location is required."];
            if (request.ReorderPoint < 0)
                errors["reorderPoint"] = ["Reorder point cannot be negative."];
            if (request.TargetStockLevel < request.ReorderPoint)
                errors["targetStockLevel"] = ["Target stock level must be at least the reorder point."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await policies.ConfigureAsync(productVariantId,
                locationId, request.ReorderPoint, request.TargetStockLevel, ct));
        }).RequireAuthorization(PermissionNames.Stock.Manage);

        return app;
    }
}

public sealed record ConfigureInventoryPolicyRequest(int ReorderPoint,
    int? TargetStockLevel);
