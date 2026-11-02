using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Products;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Products.Queries.GetProduct;

public sealed class GetProductHandler : IRequestHandler<GetProductQuery, ProductResponse>
{
    private readonly IProductRepository _products;

    public GetProductHandler(IProductRepository products) => _products = products;

    public async ValueTask<ProductResponse> Handle(GetProductQuery request, CancellationToken ct) =>
        (await _products.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Product", request.Id)).ToResponse();
}
