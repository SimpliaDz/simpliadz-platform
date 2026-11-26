namespace Simplia.Inventory.Core.Entities.Catalog;

public sealed class ProductAttribute
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    private readonly List<AttributeValue> _values = [];
    public IReadOnlyCollection<AttributeValue> Values => _values.AsReadOnly();

    private ProductAttribute() { }

    public ProductAttribute(string name) => SetName(name);

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Attribute name is required.", nameof(name));
        Name = name.Trim();
        if (Name.Length > 150)
            throw new ArgumentException("Attribute name cannot exceed 150 characters.", nameof(name));
    }

    public AttributeValue AddValue(string value, int displayOrder)
    {
        if (_values.Any(item => item.Value.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This attribute already contains the value.");
        var item = new AttributeValue(Id, value, displayOrder);
        _values.Add(item);
        return item;
    }
}
