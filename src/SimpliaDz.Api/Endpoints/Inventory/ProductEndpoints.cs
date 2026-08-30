using MediatR;

using Microsoft.AspNetCore.Mvc;

using SimpliaDz.Inventory.Contracts.Products;
using SimpliaDz.Inventory.UseCases.Products.Commands.CreateProduct;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products")
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

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

        return app;
    }
}
