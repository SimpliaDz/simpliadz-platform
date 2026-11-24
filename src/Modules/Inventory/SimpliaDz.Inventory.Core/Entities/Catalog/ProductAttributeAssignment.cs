namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class ProductAttributeAssignment
{
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public Guid AttributeId { get; private set; }
    public ProductAttribute Attribute { get; private set; } = default!;

    private ProductAttributeAssignment() { }

    public ProductAttributeAssignment(Guid productId, Guid attributeId)
    {
        ProductId = productId;
        AttributeId = attributeId;
    }
}
