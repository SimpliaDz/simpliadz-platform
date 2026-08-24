namespace SimpliaDz.Organization.Core.Entities;

public sealed class Organization
{
    public const string DefaultCurrencyCode = "DZD";
    public static readonly Guid ProfileId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; private set; } = ProfileId;
    public string Name { get; private set; } = string.Empty;
    public string? LegalName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string CurrencyCode { get; private set; } = DefaultCurrencyCode;
    public string? DefaultLanguage { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Organization()
    {
    }

    public Organization(
        string name,
        string? legalName,
        string? phone,
        string? email,
        string? address,
        string? currencyCode,
        string? defaultLanguage,
        bool isActive)
    {
        var now = DateTimeOffset.UtcNow;
        CreatedAt = now;
        Apply(name, legalName, phone, email, address, currencyCode, defaultLanguage, isActive, now);
    }

    public void Update(
        string name,
        string? legalName,
        string? phone,
        string? email,
        string? address,
        string? currencyCode,
        string? defaultLanguage,
        bool isActive) =>
        Apply(
            name,
            legalName,
            phone,
            email,
            address,
            currencyCode,
            defaultLanguage,
            isActive,
            DateTimeOffset.UtcNow);

    private void Apply(
        string name,
        string? legalName,
        string? phone,
        string? email,
        string? address,
        string? currencyCode,
        string? defaultLanguage,
        bool isActive,
        DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name is required.", nameof(name));

        var normalizedCurrency = string.IsNullOrWhiteSpace(currencyCode)
            ? DefaultCurrencyCode
            : currencyCode.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3 || !normalizedCurrency.All(char.IsAsciiLetter))
            throw new ArgumentException("Currency must be a three-letter currency code.", nameof(currencyCode));

        Name = name.Trim();
        LegalName = NormalizeOptional(legalName);
        Phone = NormalizeOptional(phone);
        Email = NormalizeOptional(email);
        Address = NormalizeOptional(address);
        CurrencyCode = normalizedCurrency;
        DefaultLanguage = NormalizeOptional(defaultLanguage);
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
