using Mediator;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.Contracts.Variants;
using SimpliaDz.Inventory.UseCases.Variants;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class VariantEndpoints
{
    public static IEndpointRouteBuilder MapVariantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/variants")
            .WithTags("Product Variants")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetVariantQuery(id), ct)))
            .RequireAuthorization(PermissionNames.Variants.View);
        group.MapGet("/by-sku/{sku}", async (string sku, [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetVariantBySkuQuery(sku), ct)))
            .RequireAuthorization(PermissionNames.Variants.View);
        group.MapGet("/by-barcode/{barcode}", async (string barcode, [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetVariantByBarcodeQuery(barcode), ct)))
            .RequireAuthorization(PermissionNames.Variants.View);

        group.MapPost("/products/{productId:guid}", async (Guid productId, [FromBody] CreateVariantRequest request,
            [FromServices] ISender sender, CancellationToken ct) =>
        {
            var variant = await sender.Send(new CreateVariantCommand(productId, request.Sku,
                request.Barcode, request.RetailPrice, request.WholesalePrice, request.AttributeValueIds), ct);
            return Results.Created($"/variants/{variant.Id}", variant);
        }).RequireAuthorization(PermissionNames.Variants.Create);

        group.MapPut("/{id:guid}/deactivate", async (Guid id, [FromServices] ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateVariantCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Variants.Edit);

        group.MapPost("/{id:guid}/generate-barcode", async (Guid id,
            [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GenerateVariantBarcodeCommand(id), ct)))
            .RequireAuthorization(PermissionNames.Variants.Edit);

        return app;
    }
}
