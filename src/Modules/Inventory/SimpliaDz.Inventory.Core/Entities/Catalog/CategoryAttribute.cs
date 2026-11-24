namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class CategoryAttribute
{
    public Guid CategoryId { get; private set; }
    public ProductCategory Category { get; private set; } = default!;
    public Guid AttributeId { get; private set; }
    public ProductAttribute Attribute { get; private set; } = default!;

    private CategoryAttribute() { }

    public CategoryAttribute(Guid categoryId, Guid attributeId)
    {
        CategoryId = categoryId;
        AttributeId = attributeId;
    }
}
