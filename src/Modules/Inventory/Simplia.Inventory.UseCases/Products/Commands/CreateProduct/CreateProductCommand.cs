using Mediator;

namespace Simplia.Inventory.UseCases.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string? Code,
    string? Description,
    Guid? BrandId,
    Guid? CategoryId
) : IRequest<Guid>;
