namespace SimpliaDz.Inventory.Contracts.Categories;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    Guid? ParentCategoryId,
    bool IsActive,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CategoryResponse> Children);
