using Mediator;
using Simplia.Inventory.Contracts.Products;

namespace Simplia.Inventory.UseCases.Products.Queries.GetProduct;

public sealed record GetProductQuery(Guid Id) : IRequest<ProductResponse>;
