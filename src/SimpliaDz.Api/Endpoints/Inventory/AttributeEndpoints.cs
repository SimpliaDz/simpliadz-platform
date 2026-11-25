using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.Contracts.Attributes;
using SimpliaDz.Inventory.UseCases.Attributes;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class AttributeEndpoints
{
    public static IEndpointRouteBuilder MapAttributeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/attributes")
            .WithTags("Product Attributes")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async ([FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAttributesQuery(), ct)))
            .RequireAuthorization(PermissionNames.Attributes.View);

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAttributeQuery(id), ct)))
            .RequireAuthorization(PermissionNames.Attributes.View);

        group.MapPost("/", async ([FromBody] CreateAttributeRequest request, [FromServices] ISender sender, CancellationToken ct) =>
        {
            var attribute = await sender.Send(new CreateAttributeCommand(request.Name), ct);
            return Results.Created($"/attributes/{attribute.Id}", attribute);
        }).RequireAuthorization(PermissionNames.Attributes.Manage);

        group.MapPost("/{id:guid}/values", async (Guid id, [FromBody] CreateAttributeValueRequest request,
            [FromServices] ISender sender, CancellationToken ct) =>
        {
            var value = await sender.Send(new CreateAttributeValueCommand(id, request.Value, request.DisplayOrder), ct);
            return Results.Created($"/attributes/{id}/values/{value.Id}", value);
        }).RequireAuthorization(PermissionNames.Attributes.Manage);

        group.MapPut("/{id:guid}/values/{valueId:guid}/order", async (Guid id, Guid valueId,
            [FromBody] UpdateDisplayOrderRequest request, [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new SetAttributeValueOrderCommand(id, valueId, request.DisplayOrder), ct)))
            .RequireAuthorization(PermissionNames.Attributes.Manage);

        group.MapPut("/{id:guid}/values/{valueId:guid}", async (Guid id, Guid valueId,
            [FromBody] RenameAttributeValueRequest request, [FromServices] InventoryDbContext db,
            CancellationToken ct) =>
        {
            var current = await db.AttributeValues.SingleOrDefaultAsync(x => x.Id == valueId && x.AttributeId == id, ct)
                ?? throw new NotFoundException("Attribute value", valueId);
            current.Rename(request.Value);
            if (await db.AttributeValues.AnyAsync(x => x.AttributeId == id && x.Id != valueId &&
                    x.Value == current.Value, ct))
                throw new ConflictException("This attribute already contains that value.");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Attributes.Manage);

        group.MapDelete("/{id:guid}/values/{valueId:guid}", async (Guid id, Guid valueId,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var current = await db.AttributeValues.SingleOrDefaultAsync(x => x.Id == valueId && x.AttributeId == id, ct)
                ?? throw new NotFoundException("Attribute value", valueId);
            if (await db.ProductVariants.AnyAsync(variant => variant.AttributeValues
                .Any(value => value.AttributeValueId == valueId), ct))
                throw new ConflictException("This value is used by product variants. Replace it on those variants before deleting it.");
            db.AttributeValues.Remove(current);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 547 })
            {
                throw new ConflictException("This value is used by product variants. Replace it before deleting it.");
            }
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Attributes.Manage);

        return app;
    }
}

public sealed record RenameAttributeValueRequest(string Value);
