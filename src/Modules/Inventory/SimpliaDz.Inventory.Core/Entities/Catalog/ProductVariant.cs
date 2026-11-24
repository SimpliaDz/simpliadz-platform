using SimpliaDz.Inventory.Core.ValueObjects;

namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class ProductVariant
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;

    public Sku Sku { get; private set; }

    public string? Barcode { get; private set; }
    public string? Color { get; private set; }
    public string? Size { get; private set; }

    public decimal RetailPrice { get; private set; }
    public decimal? WholesalePrice { get; private set; }

    public bool IsActive { get; private set; } = true;
    private readonly List<VariantAttributeValue> _attributeValues = [];
    public IReadOnlyCollection<VariantAttributeValue> AttributeValues => _attributeValues.AsReadOnly();

    private ProductVariant() { }

    public ProductVariant(
        Guid productId,
        Sku sku,
        decimal retailPrice,
        decimal? wholesalePrice = null,
        string? barcode = null,
        string? color = null,
        string? size = null)
    {
        ProductId = productId;

        SetSku(sku);
        SetBarcode(barcode);
        SetColor(color);
        SetSize(size);

        SetRetailPrice(retailPrice);
        SetWholesalePrice(wholesalePrice);
    }

    public void SetSku(Sku sku)
    {
        Sku = sku;
    }

    public void SetBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            Barcode = null;
            return;
        }

        var trimmed = barcode.Trim();

        if (trimmed.Length > 64)
            throw new ArgumentException("Barcode cannot exceed 64 characters.", nameof(barcode));

        Barcode = trimmed;
    }

    public string GenerateInternalBarcode()
    {
        if (!IsActive) throw new InvalidOperationException("Cannot generate a barcode for an inactive variant.");
        if (Barcode is not null) throw new InvalidOperationException("Variant already has a barcode.");
        SetBarcode(InternalBarcode.Generate());
        return Barcode!;
    }

    public void SetColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            Color = null;
            return;
        }

        var trimmed = color.Trim();

        if (trimmed.Length > 64)
            throw new ArgumentException("Color cannot exceed 64 characters.", nameof(color));

        Color = trimmed;
    }

    public void SetSize(string? size)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            Size = null;
            return;
        }

        var trimmed = size.Trim();

        if (trimmed.Length > 32)
            throw new ArgumentException("Size cannot exceed 32 characters.", nameof(size));

        Size = trimmed;
    }

    public void SetRetailPrice(decimal price)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        RetailPrice = price;
    }

    public void SetWholesalePrice(decimal? price)
    {
        if (price is < 0) throw new ArgumentOutOfRangeException(nameof(price));
        WholesalePrice = price;
    }

    public void AssignAttributeValues(IEnumerable<Guid> valueIds)
    {
        var ids = valueIds.ToArray();
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Attribute values must be distinct and valid.", nameof(valueIds));

        _attributeValues.Clear();
        foreach (var id in ids)
            _attributeValues.Add(new VariantAttributeValue(Id, id));
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
