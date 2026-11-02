namespace SimpliaDz.Inventory.Contracts.Categories;

public sealed record CreateCategoryRequest(string Name, Guid? ParentCategoryId);
