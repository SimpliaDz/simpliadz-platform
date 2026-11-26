namespace Simplia.Sales.Core.Entities;

public sealed class CashRegister
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid LocationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private CashRegister() { }

    public CashRegister(Guid locationId, string code, string name)
    {
        if (locationId == Guid.Empty) throw new ArgumentException("Location is required.", nameof(locationId));
        LocationId = locationId;
        Update(code, name);
    }

    public void Update(string code, string name)
    {
        Code = Required(code, 40, nameof(code)).ToUpperInvariant();
        Name = Required(name, 100, nameof(name));
    }

    public void Deactivate() => IsActive = false;

    private static string Required(string value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        var result = value.Trim();
        if (result.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", name);
        return result;
    }
}
