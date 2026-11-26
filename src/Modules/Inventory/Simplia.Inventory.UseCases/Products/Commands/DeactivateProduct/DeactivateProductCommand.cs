using Mediator;

namespace Simplia.Inventory.UseCases.Products.Commands.DeactivateProduct;

public sealed record DeactivateProductCommand(Guid Id) : IRequest;
