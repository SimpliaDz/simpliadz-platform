namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class ProductCategory
{
    #region Properties

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    #endregion

    private ProductCategory() { }

    public ProductCategory(string name) => SetName(name);

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
    public void Activate()
    {
        IsActive = true;
        //Touch(); for later make this generic to add date and Byuser ... 
    }

    public void Deactivate()
    {
        IsActive = false;
        //Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
