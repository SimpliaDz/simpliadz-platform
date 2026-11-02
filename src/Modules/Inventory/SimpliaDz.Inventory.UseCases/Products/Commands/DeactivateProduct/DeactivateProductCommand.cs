using Mediator;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.DeactivateProduct;

public sealed record DeactivateProductCommand(Guid Id) : IRequest;
