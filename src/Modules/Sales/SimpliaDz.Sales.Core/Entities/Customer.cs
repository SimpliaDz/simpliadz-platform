using System.Net.Mail;

namespace SimpliaDz.Sales.Core.Entities;

public enum CustomerType
{
    Retail = 1,
    Wholesale = 2
}

public sealed class Customer
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public CustomerType CustomerType { get; private set; } = CustomerType.Retail;
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; private set; } = [];

    private Customer() { }

    public Customer(string name, string? phone, string? email, string? address,
        CustomerType customerType, string? notes) =>
        Update(name, phone, email, address, customerType, notes);

    public void Update(string name, string? phone, string? email, string? address,
        CustomerType customerType, string? notes)
    {
        var normalizedName = NormalizeRequired(name, 150, nameof(name));
        var normalizedPhone = NormalizeOptional(phone, 50, nameof(phone));
        var normalizedEmail = NormalizeOptional(email, 254, nameof(email));
        var normalizedAddress = NormalizeOptional(address, 500, nameof(address));
        var normalizedNotes = NormalizeOptional(notes, 2000, nameof(notes));
        if (!Enum.IsDefined(customerType))
            throw new ArgumentOutOfRangeException(nameof(customerType));
        if (normalizedEmail is not null &&
            (!MailAddress.TryCreate(normalizedEmail, out var parsed) || parsed.Address != normalizedEmail))
            throw new ArgumentException("A valid customer email is required.", nameof(email));

        Name = normalizedName;
        Phone = normalizedPhone;
        Email = normalizedEmail;
        Address = normalizedAddress;
        CustomerType = customerType;
        Notes = normalizedNotes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeRequired(string? value, int maxLength, string name) =>
        NormalizeOptional(value, maxLength, name)
        ?? throw new ArgumentException("Customer name is required.", name);

    private static string? NormalizeOptional(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", name);
        return normalized;
    }
}
