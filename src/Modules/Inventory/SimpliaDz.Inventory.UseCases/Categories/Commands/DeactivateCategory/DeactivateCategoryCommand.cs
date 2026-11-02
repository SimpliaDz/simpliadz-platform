using Mediator;

namespace SimpliaDz.Inventory.UseCases.Categories.Commands.DeactivateCategory;

public sealed record DeactivateCategoryCommand(Guid Id) : IRequest;
