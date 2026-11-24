namespace SimpliaDz.Inventory.Contracts.Categories;

public sealed record UpdateCategoryRequest(string Name, Guid? ParentCategoryId, IReadOnlyList<Guid>? AttributeIds = null, IReadOnlyList<Guid>? RequiredAttributeIds = null);
