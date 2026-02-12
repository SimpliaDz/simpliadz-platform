namespace SimpliaDz.Inventory.Core.Entities.Stock;

public sealed class InventoryLocation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Code { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;


    private InventoryLocation() { }

    public InventoryLocation(string name, string? code = null)
    {
        SetName(name);
        SetCode(code);
    }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Location name is required.", nameof(name));

        var trimmed = name.Trim();

        if (trimmed.Length > 150)
            throw new ArgumentException("Location name cannot exceed 150 characters.", nameof(name));

        Name = trimmed;
        Touch();
    }

    public void SetCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            Code = null;
            return;
        }

        var trimmed = code.Trim();

        if (trimmed.Length > 50)
            throw new ArgumentException("Location code cannot exceed 50 characters.", nameof(code));

        Code = trimmed.ToUpperInvariant();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
