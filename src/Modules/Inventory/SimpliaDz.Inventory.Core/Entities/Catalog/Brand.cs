namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class Brand
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;


    private Brand() { }

    public Brand(string name) => SetName(name);

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Brand name is required.", nameof(name));

        var trimmed = name.Trim();

        if (trimmed.Length > 150)
            throw new ArgumentException("Brand name cannot exceed 150 characters.", nameof(name));

        Name = trimmed;
        Touch();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
