using Mediator;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.ActivateProduct;

public sealed record ActivateProductCommand(Guid Id) : IRequest;
