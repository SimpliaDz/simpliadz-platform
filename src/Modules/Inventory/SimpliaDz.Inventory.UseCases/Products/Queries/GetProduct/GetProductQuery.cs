using Mediator;
using SimpliaDz.Inventory.Contracts.Products;

namespace SimpliaDz.Inventory.UseCases.Products.Queries.GetProduct;

public sealed record GetProductQuery(Guid Id) : IRequest<ProductResponse>;
