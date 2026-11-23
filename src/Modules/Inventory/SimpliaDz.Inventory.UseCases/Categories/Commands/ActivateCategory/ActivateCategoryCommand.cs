using Mediator;
namespace SimpliaDz.Inventory.UseCases.Categories.Commands.ActivateCategory;
public sealed record ActivateCategoryCommand(Guid Id) : IRequest;
