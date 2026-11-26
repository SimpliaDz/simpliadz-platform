namespace Simplia.Inventory.Contracts.Categories;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    Guid? ParentCategoryId,
    bool IsActive,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Guid> AttributeIds,
    IReadOnlyList<Guid> RequiredAttributeIds,
    IReadOnlyList<CategoryResponse> Children);
