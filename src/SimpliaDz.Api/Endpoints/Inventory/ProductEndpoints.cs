using Mediator;

using Microsoft.AspNetCore.Mvc;

using SimpliaDz.Inventory.Contracts.Products;
using SimpliaDz.Inventory.UseCases.Products.Commands.CreateProduct;
using SimpliaDz.Inventory.UseCases.Products.Commands.ActivateProduct;
using SimpliaDz.Inventory.UseCases.Products.Commands.DeactivateProduct;
using SimpliaDz.Inventory.UseCases.Products.Commands.UpdateProduct;
using SimpliaDz.Inventory.UseCases.Products.Queries.GetProduct;
using SimpliaDz.Inventory.UseCases.Products.Queries.GetProducts;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products")
            .WithTags("Products")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var products = await sender.Send(new GetProductsQuery(search, isActive), ct);
            return Results.Ok(products);
        }).RequireAuthorization(PermissionNames.Products.View);

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var product = await sender.Send(new GetProductQuery(id), ct);
            return Results.Ok(product);
        }).RequireAuthorization(PermissionNames.Products.View);

        group.MapPost("/", async (
            [FromBody] CreateProductRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateProductCommand(
                    request.Name,
                    request.Code,
                    request.Description,
                    request.BrandId,
                    request.CategoryId),
                ct);

            return Results.Created($"/products/{id}", new CreateProductResponse(id));
        }).RequireAuthorization(PermissionNames.Products.Create);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProductRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var product = await sender.Send(new UpdateProductCommand(
                id,
                request.Name,
                request.Code,
                request.Description,
                request.BrandId,
                request.CategoryId), ct);

            return Results.Ok(product);
        }).RequireAuthorization(PermissionNames.Products.Edit);

        group.MapPut("/{id:guid}/deactivate", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new DeactivateProductCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Products.Edit);

        group.MapPut("/{id:guid}/activate", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ActivateProductCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Products.Edit);

        return app;
    }
}
