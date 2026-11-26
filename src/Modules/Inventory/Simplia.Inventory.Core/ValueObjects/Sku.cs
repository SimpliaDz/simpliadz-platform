using Simplia.Inventory.Core.Exceptions;

namespace Simplia.Inventory.Core.ValueObjects;

public readonly record struct Sku
{
    public const int MaxLength = 64;

    public string Value { get; }

    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidSkuException("SKU is required.");

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
            throw new InvalidSkuException($"SKU cannot exceed {MaxLength} characters.");

        Value = trimmed;
    }

    public override string ToString() => Value;

    public static implicit operator string(Sku sku) => sku.Value;
}
