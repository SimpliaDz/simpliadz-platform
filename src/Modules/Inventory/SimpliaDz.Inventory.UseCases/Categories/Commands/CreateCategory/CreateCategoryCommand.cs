using Mediator;
using SimpliaDz.Inventory.Contracts.Categories;

namespace SimpliaDz.Inventory.UseCases.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(
    string Name,
    Guid? ParentCategoryId, IReadOnlyList<Guid>? AttributeIds = null) : IRequest<CategoryResponse>;
