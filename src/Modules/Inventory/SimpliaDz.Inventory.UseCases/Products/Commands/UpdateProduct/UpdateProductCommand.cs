using Mediator;
using SimpliaDz.Inventory.Contracts.Products;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    Guid? BrandId,
    Guid? CategoryId) : IRequest<ProductResponse>;
