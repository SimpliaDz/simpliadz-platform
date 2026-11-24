using Mediator;
using SimpliaDz.Inventory.Contracts.Categories;

namespace SimpliaDz.Inventory.UseCases.Categories.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    Guid? ParentCategoryId, IReadOnlyList<Guid>? AttributeIds = null) : IRequest<CategoryResponse>;
