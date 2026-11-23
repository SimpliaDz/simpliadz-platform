using System.Security.Cryptography;

namespace SimpliaDz.Inventory.Core.Entities.Catalog;

/// <summary>
/// Generates restricted-circulation EAN-13 numbers for use inside a tenant.
/// These are not globally assigned GS1 product identifiers.
/// </summary>
public static class InternalBarcode
{
    private const string InternalPrefix = "20";

    public static string Generate()
    {
        Span<char> digits = stackalloc char[13];
        InternalPrefix.AsSpan().CopyTo(digits);
        for (var i = 2; i < 12; i++)
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        digits[12] = (char)('0' + CheckDigit(digits[..12]));
        return new string(digits);
    }

    public static bool IsValidEan13(string? value)
    {
        if (value is not { Length: 13 }) return false;
        foreach (var digit in value)
            if (digit is < '0' or > '9') return false;
        return value[12] - '0' == CheckDigit(value.AsSpan(0, 12));
    }

    private static int CheckDigit(ReadOnlySpan<char> digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }
}
