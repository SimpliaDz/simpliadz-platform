using Mediator;
using Simplia.Inventory.Contracts.Categories;

namespace Simplia.Inventory.UseCases.Categories.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    Guid? ParentCategoryId, IReadOnlyList<Guid>? AttributeIds = null, IReadOnlyList<Guid>? RequiredAttributeIds = null) : IRequest<CategoryResponse>;
