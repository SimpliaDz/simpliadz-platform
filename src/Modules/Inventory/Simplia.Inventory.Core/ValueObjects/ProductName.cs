using Simplia.Inventory.Core.Exceptions;

namespace Simplia.Inventory.Core.ValueObjects;

public readonly record struct ProductName
{
    public const int MaxLength = 200;

    public string Value { get; }

    public ProductName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidProductNameException("Product name is required.");

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
            throw new InvalidProductNameException($"Product name cannot exceed {MaxLength} characters.");

        Value = trimmed;
    }

    public override string ToString() => Value;

    public static implicit operator string(ProductName name) => name.Value;
}
