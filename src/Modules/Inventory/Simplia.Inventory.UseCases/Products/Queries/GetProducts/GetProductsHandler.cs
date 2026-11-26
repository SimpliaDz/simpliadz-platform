using Mediator;
using Simplia.Inventory.Contracts.Products;
using Simplia.Inventory.UseCases.Products.Abstractions;

namespace Simplia.Inventory.UseCases.Products.Queries.GetProducts;

public sealed class GetProductsHandler : IRequestHandler<GetProductsQuery, IReadOnlyList<ProductResponse>>
{
    private readonly IProductRepository _products;

    public GetProductsHandler(IProductRepository products) => _products = products;

    public async ValueTask<IReadOnlyList<ProductResponse>> Handle(GetProductsQuery request, CancellationToken ct) =>
        (await _products.SearchAsync(request.Search, request.IsActive, ct))
            .Select(product => product.ToResponse())
            .ToArray();
}
