using Mediator;
using SimpliaDz.Inventory.Contracts.Products;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Products.Queries.GetProducts;

public sealed class GetProductsHandler : IRequestHandler<GetProductsQuery, IReadOnlyList<ProductResponse>>
{
    private readonly IProductRepository _products;

    public GetProductsHandler(IProductRepository products) => _products = products;

    public async ValueTask<IReadOnlyList<ProductResponse>> Handle(GetProductsQuery request, CancellationToken ct) =>
        (await _products.SearchAsync(request.Search, request.IsActive, ct))
            .Select(product => product.ToResponse())
            .ToArray();
}
