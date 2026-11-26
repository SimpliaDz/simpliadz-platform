using Mediator;
using Microsoft.EntityFrameworkCore;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Audit.UseCases;
using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Inventory.Contracts.Prices;
using Simplia.Inventory.UseCases.Prices;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Inventory;

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

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] RenamePriceListRequest request, HttpContext http,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var list = await db.PriceLists.FindAsync([id], ct) ?? throw new NotFoundException("Price list", id);
            var before = list.Name;
            list.Rename(request.Name);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
            {
                throw new ConflictException("A price list already uses that name and currency.");
            }
            if (before != list.Name)
                await BusinessAudit.RecordIfEnabledAsync(http, "PriceListRenamed", "PriceList", id,
                    new Dictionary<string, object?> { ["previousName"] = before, ["name"] = list.Name }, ct);
            return Results.NoContent();
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization(PermissionNames.PriceLists.Manage);

        group.MapPut("/{id:guid}/deactivate", async (Guid id, HttpContext http,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var list = await db.PriceLists.FindAsync([id], ct) ?? throw new NotFoundException("Price list", id);
            if (!list.IsActive) return Results.NoContent();
            list.Deactivate();
            await db.SaveChangesAsync(ct);
            await BusinessAudit.RecordIfEnabledAsync(http, "PriceListDeactivated", "PriceList", id,
                new Dictionary<string, object?> { ["name"] = list.Name, ["isActive"] = false }, ct);
            return Results.NoContent();
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization(PermissionNames.PriceLists.Manage);

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

public sealed record RenamePriceListRequest(string Name);
