using MediatR;

using SimpliaDz.Inventory.Contracts.Products;
using SimpliaDz.Inventory.UseCases.Products.Commands.CreateProduct;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        group.MapPost("/", async (
            CreateProductRequest request,
            ISender sender,
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
        });

        return app;
    }
}