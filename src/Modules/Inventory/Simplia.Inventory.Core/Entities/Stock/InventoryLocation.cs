namespace Simplia.Inventory.Core.Entities.Stock;

public sealed class InventoryLocation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Code { get; private set; }
    public LocationType Type { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private InventoryLocation() { }

    public InventoryLocation(
        string name,
        string? code = null,
        LocationType type = LocationType.Store,
        string? address = null,
        string? phone = null)
    {
        Update(name, code, type, address, phone);
    }

    public void Update(
        string name,
        string? code,
        LocationType type,
        string? address,
        string? phone)
    {
        SetName(name);
        SetCode(code);
        SetType(type);
        Address = NormalizeOptional(address, 500, nameof(address));
        Phone = NormalizeOptional(phone, 50, nameof(phone));
        Touch();
    }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Location name is required.", nameof(name));

        var trimmed = name.Trim();

        if (trimmed.Length > 150)
            throw new ArgumentException("Location name cannot exceed 150 characters.", nameof(name));

        Name = trimmed;
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
            throw new ArgumentException("Location code cannot exceed 50 characters.", nameof(code));

        Code = trimmed.ToUpperInvariant();
        Touch();
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

    private void SetType(LocationType type)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), "Unsupported location type.");

        Type = type;
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        if (trimmed.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);

        return trimmed;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
