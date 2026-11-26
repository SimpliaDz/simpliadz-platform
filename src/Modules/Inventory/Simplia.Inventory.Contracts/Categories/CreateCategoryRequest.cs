namespace Simplia.Inventory.Contracts.Categories;

public sealed record CreateCategoryRequest(string Name, Guid? ParentCategoryId, IReadOnlyList<Guid>? AttributeIds = null, IReadOnlyList<Guid>? RequiredAttributeIds = null);
