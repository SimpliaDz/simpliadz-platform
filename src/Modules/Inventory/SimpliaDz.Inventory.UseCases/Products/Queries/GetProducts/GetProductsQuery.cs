using Mediator;
using SimpliaDz.Inventory.Contracts.Products;

namespace SimpliaDz.Inventory.UseCases.Products.Queries.GetProducts;

public sealed record GetProductsQuery(string? Search, bool? IsActive) : IRequest<IReadOnlyList<ProductResponse>>;
