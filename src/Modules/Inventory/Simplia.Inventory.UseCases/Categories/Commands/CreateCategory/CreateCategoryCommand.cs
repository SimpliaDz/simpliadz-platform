using Mediator;
using Simplia.Inventory.Contracts.Categories;

namespace Simplia.Inventory.UseCases.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(
    string Name,
    Guid? ParentCategoryId, IReadOnlyList<Guid>? AttributeIds = null, IReadOnlyList<Guid>? RequiredAttributeIds = null) : IRequest<CategoryResponse>;
