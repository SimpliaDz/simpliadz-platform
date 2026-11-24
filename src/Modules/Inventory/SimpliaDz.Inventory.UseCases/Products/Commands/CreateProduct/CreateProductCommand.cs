using Mediator;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string? Code,
    string? Description,
    Guid? BrandId,
    Guid? CategoryId,
    IReadOnlyList<Guid>? AttributeIds = null
) : IRequest<Guid>;
