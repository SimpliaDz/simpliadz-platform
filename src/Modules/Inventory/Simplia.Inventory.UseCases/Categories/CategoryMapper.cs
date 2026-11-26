using Simplia.Inventory.Contracts.Categories;
using Simplia.Inventory.Core.Entities.Catalog;

namespace Simplia.Inventory.UseCases.Categories;

internal static class CategoryMapper
{
    public static CategoryResponse ToResponse(this ProductCategory category) =>
        new(
            category.Id,
            category.Name,
            category.ParentCategoryId,
            category.IsActive,
            category.UpdatedAt,
            category.Attributes.Select(attribute => attribute.AttributeId).ToArray(),
            category.Attributes.Where(attribute => attribute.IsRequired).Select(attribute => attribute.AttributeId).ToArray(),
            []);

    public static IReadOnlyList<CategoryResponse> ToHierarchy(
        this IReadOnlyList<ProductCategory> categories)
    {
        var childrenByParent = categories
            .GroupBy(category => category.ParentCategoryId ?? Guid.Empty)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase).ToArray());

        var visited = new HashSet<Guid>();
        var roots = BuildChildren(Guid.Empty, childrenByParent, visited);

        foreach (var category in categories.OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!visited.Contains(category.Id))
                roots.Add(BuildNode(category, childrenByParent, visited));
        }

        return roots;
    }

    public static CategoryResponse? FindSubtree(
        this IReadOnlyList<ProductCategory> categories,
        Guid id)
    {
        var childrenByParent = categories
            .GroupBy(category => category.ParentCategoryId ?? Guid.Empty)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var category = categories.FirstOrDefault(item => item.Id == id);

        return category is null
            ? null
            : BuildNode(category, childrenByParent, []);
    }

    private static List<CategoryResponse> BuildChildren(
        Guid parentId,
        IReadOnlyDictionary<Guid, ProductCategory[]> childrenByParent,
        HashSet<Guid> visited)
    {
        if (!childrenByParent.TryGetValue(parentId, out var children))
            return [];

        return children
            .Where(child => !visited.Contains(child.Id))
            .Select(child => BuildNode(child, childrenByParent, visited))
            .ToList();
    }

    private static CategoryResponse BuildNode(
        ProductCategory category,
        IReadOnlyDictionary<Guid, ProductCategory[]> childrenByParent,
        HashSet<Guid> visited)
    {
        visited.Add(category.Id);
        var children = BuildChildren(category.Id, childrenByParent, visited).ToArray();

        return new CategoryResponse(
            category.Id,
            category.Name,
            category.ParentCategoryId,
            category.IsActive,
            category.UpdatedAt,
            category.Attributes.Select(attribute => attribute.AttributeId).ToArray(),
            category.Attributes.Where(attribute => attribute.IsRequired).Select(attribute => attribute.AttributeId).ToArray(),
            children);
    }
}
