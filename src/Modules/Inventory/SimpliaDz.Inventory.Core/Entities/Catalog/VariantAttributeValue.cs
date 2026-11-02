namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class VariantAttributeValue
{
    public Guid VariantId { get; private set; }
    public ProductVariant Variant { get; private set; } = default!;
    public Guid AttributeValueId { get; private set; }
    public AttributeValue AttributeValue { get; private set; } = default!;

    private VariantAttributeValue() { }
    public VariantAttributeValue(Guid variantId, Guid attributeValueId)
    {
        VariantId = variantId;
        AttributeValueId = attributeValueId;
    }
}
