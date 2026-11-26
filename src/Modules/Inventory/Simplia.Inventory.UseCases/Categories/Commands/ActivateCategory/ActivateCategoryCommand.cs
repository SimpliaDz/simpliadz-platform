using Mediator;
namespace Simplia.Inventory.UseCases.Categories.Commands.ActivateCategory;
public sealed record ActivateCategoryCommand(Guid Id) : IRequest;
