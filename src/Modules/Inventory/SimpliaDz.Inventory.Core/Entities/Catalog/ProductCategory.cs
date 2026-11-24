namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class ProductCategory
{
    #region Properties

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public Guid? ParentCategoryId { get; private set; }
    public ProductCategory? ParentCategory { get; private set; }
    private readonly List<ProductCategory> _children = [];
    public IReadOnlyCollection<ProductCategory> Children => _children.AsReadOnly();
    private readonly List<CategoryAttribute> _attributes = [];
    public IReadOnlyCollection<CategoryAttribute> Attributes => _attributes.AsReadOnly();
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    #endregion

    private ProductCategory() { }

    public ProductCategory(string name, Guid? parentCategoryId = null)
    {
        SetName(name);
        SetParent(parentCategoryId);
    }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.", nameof(name));

        var trimmed = name.Trim();

        if (trimmed.Length > 150)
            throw new ArgumentException("Category name cannot exceed 150 characters.", nameof(name));

        Name = trimmed;
        Touch();
    }

    public void Update(string name, Guid? parentCategoryId)
    {
        SetName(name);
        SetParent(parentCategoryId);
    }

    public void SetParent(Guid? parentCategoryId)
    {
        if (parentCategoryId == Id)
            throw new InvalidOperationException("A category cannot be its own parent.");

        ParentCategoryId = parentCategoryId;
        Touch();
    }

    public void AssignAttributes(IEnumerable<Guid> attributeIds, IEnumerable<Guid>? requiredAttributeIds = null)
    {
        var ids = attributeIds.Distinct().ToArray();
        if (ids.Any(id => id == Guid.Empty))
            throw new ArgumentException("Attributes must be valid.", nameof(attributeIds));
        var requiredIds = (requiredAttributeIds ?? []).ToHashSet();
        if (!requiredIds.IsSubsetOf(ids))
            throw new ArgumentException("Required attributes must be selected for the category.", nameof(requiredAttributeIds));
        _attributes.Clear();
        foreach (var id in ids)
            _attributes.Add(new CategoryAttribute(Id, id, requiredIds.Contains(id)));
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
