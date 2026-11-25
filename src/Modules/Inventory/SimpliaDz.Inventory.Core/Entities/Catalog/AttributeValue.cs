namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class AttributeValue
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid AttributeId { get; private set; }
    public ProductAttribute Attribute { get; private set; } = default!;
    public string Value { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }

    private AttributeValue() { }

    public AttributeValue(Guid attributeId, string value, int displayOrder)
    {
        if (attributeId == Guid.Empty) throw new ArgumentException("Attribute is required.", nameof(attributeId));
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        AttributeId = attributeId;
        Rename(value);
        DisplayOrder = displayOrder;
    }

    public void Rename(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 150) throw new ArgumentException("Value cannot exceed 150 characters.", nameof(value));
        Value = normalized;
    }

    public void SetDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        DisplayOrder = displayOrder;
    }
}
