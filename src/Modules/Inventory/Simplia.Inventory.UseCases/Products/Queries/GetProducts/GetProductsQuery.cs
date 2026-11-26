using Mediator;
using Simplia.Inventory.Contracts.Products;

namespace Simplia.Inventory.UseCases.Products.Queries.GetProducts;

public sealed record GetProductsQuery(string? Search, bool? IsActive) : IRequest<IReadOnlyList<ProductResponse>>;
