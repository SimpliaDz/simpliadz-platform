using Mediator;

namespace Simplia.Inventory.UseCases.Categories.Commands.DeactivateCategory;

public sealed record DeactivateCategoryCommand(Guid Id) : IRequest;
