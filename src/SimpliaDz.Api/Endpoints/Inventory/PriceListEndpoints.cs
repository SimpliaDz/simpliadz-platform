using Mediator;
using SimpliaDz.Audit.UseCases;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.Contracts.Prices;
using SimpliaDz.Inventory.UseCases.Prices;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class PriceListEndpoints
{
    public static IEndpointRouteBuilder MapPriceListEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/price-lists")
            .WithTags("Price Lists")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async ([FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetPriceListsQuery(), ct)))
            .RequireAuthorization(PermissionNames.PriceLists.View);

        group.MapPost("/", async ([FromBody] CreatePriceListRequest request,
            [FromServices] ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new CreatePriceListCommand(request.Name, request.CurrencyCode, request.Type), ct);
            return Results.Created($"/price-lists/{list.Id}", list);
        }).RequireAuthorization(PermissionNames.PriceLists.Manage);

        group.MapPut("/{id:guid}/variants/{variantId:guid}", async (Guid id, Guid variantId,
            [FromBody] SetVariantPriceRequest request, HttpContext http,
            [FromServices] ISender sender, [FromServices] IAuditTrail audit,
            CancellationToken ct) =>
        {
            decimal? oldPrice;
            try { oldPrice = (await sender.Send(new ResolveVariantPriceQuery(id, variantId), ct)).Price; }
            catch (NotFoundException) { oldPrice = null; }
            var result = await sender.Send(new SetVariantPriceCommand(id, variantId, request.Price), ct);
            if (oldPrice != result.Price)
                await BusinessAudit.RecordAsync(audit, http, "PriceChanged", "ProductVariant",
                    variantId,
                    new Dictionary<string, object?> { ["priceListId"] = id, ["price"] = oldPrice },
                    new Dictionary<string, object?> { ["priceListId"] = id,
                        ["price"] = result.Price, ["currencyCode"] = result.CurrencyCode }, ct);
            return Results.Ok(result);
        })
            .RequireAudit()
            .RequireAuthorization(PermissionNames.PriceLists.Manage);

        group.MapGet("/{id:guid}/variants/{variantId:guid}", async (Guid id, Guid variantId,
            [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ResolveVariantPriceQuery(id, variantId), ct)))
            .RequireAuthorization(PermissionNames.PriceLists.View);

        return app;
    }
}
