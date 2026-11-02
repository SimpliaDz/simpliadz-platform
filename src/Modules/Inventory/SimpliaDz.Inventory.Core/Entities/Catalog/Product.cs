using SimpliaDz.Inventory.Core.ValueObjects;

namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class Product
{
    #region Properties

    public Guid Id { get; private set; } = Guid.NewGuid();
    public ProductName Name { get; private set; } = new ProductName("Unnamed");
    public string? Description { get; private set; }
    // Optional internal code (not barcode). Example: "TSHIRT", "JEAN", etc.
    public string? Code { get; private set; }
    public Guid? BrandId { get; private set; }
    public Brand? Brand { get; private set; }
    public Guid? ProductCategoryId { get; private set; }
    public ProductCategory? ProductCategory { get; private set; }
    public bool IsActive { get; private set; } = true;
    private readonly List<ProductVariant> _variants = [];
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    #endregion

    // Required by EF Core (but EF is NOT referenced here)
    private Product() { }

    public Product(string name, string? code = null, string? description = null)
    {
        SetName(name);
        SetCode(code);
        SetDescription(description);

        // CreatedAt already set by default initializer
        Touch();
    }

    public void SetName(string name)
    {
        Name = new ProductName(name);
        Touch();
    }

    public void SetDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            Description = null;
            Touch();
            return;
        }

        var trimmed = description.Trim();

        if (trimmed.Length > 2000)
            throw new ArgumentException("Description cannot exceed 2000 characters.", nameof(description));

        Description = trimmed;
        Touch();
    }

    public void SetCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            Code = null;
            Touch();
            return;
        }

        var trimmed = code.Trim();

        if (trimmed.Length > 50)
            throw new ArgumentException("Code cannot exceed 50 characters.", nameof(code));

        Code = trimmed.ToUpperInvariant();
        Touch();
    }

    public void Update(
        string name,
        string? code,
        string? description,
        Guid? brandId,
        Guid? categoryId)
    {
        SetName(name);
        SetCode(code);
        SetDescription(description);
        AssignBrand(brandId);
        AssignCategory(categoryId);
    }

    public void AssignBrand(Guid? brandId)
    {
        BrandId = brandId;
        Touch();
    }

    public void AssignCategory(Guid? categoryId)
    {
        ProductCategoryId = categoryId;
        Touch();
    }

    public ProductVariant AddVariant(
        string sku,
        decimal retailPrice,
        decimal? wholesalePrice = null,
        string? barcode = null,
        string? color = null,
        string? size = null,
        IReadOnlyCollection<Guid>? attributeValueIds = null)
    {
        var newSku = new Sku(sku);

        if (_variants.Any(v => v.Sku.Value.Equals(newSku.Value, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Variant SKU '{newSku.Value}' already exists for this product.");

        var variant = new ProductVariant(
            productId: Id,
            sku: newSku,
            retailPrice: retailPrice,
            wholesalePrice: wholesalePrice,
            barcode: barcode,
            color: color,
            size: size
        );

        if (attributeValueIds is not null)
        {
            variant.AssignAttributeValues(attributeValueIds);
            if (_variants.Any(v => v.CombinationKey == variant.CombinationKey))
                throw new InvalidOperationException("This product already has a variant with the same attribute combination.");
        }

        _variants.Add(variant);
        Touch();
        return variant;
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
