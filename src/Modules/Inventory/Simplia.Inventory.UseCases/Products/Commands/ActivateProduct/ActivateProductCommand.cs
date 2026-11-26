using Mediator;

namespace Simplia.Inventory.UseCases.Products.Commands.ActivateProduct;

public sealed record ActivateProductCommand(Guid Id) : IRequest;
