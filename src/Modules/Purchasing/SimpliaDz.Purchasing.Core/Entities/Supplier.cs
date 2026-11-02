using System.Net.Mail;

namespace SimpliaDz.Purchasing.Core.Entities;

public sealed class Supplier
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; private set; } = [];

    private Supplier() { }

    public Supplier(string name, string? phone = null, string? email = null,
        string? address = null, string? notes = null) =>
        Update(name, phone, email, address, notes);

    public void Update(string name, string? phone, string? email, string? address, string? notes)
    {
        var normalizedName = NormalizeRequired(name, 150, nameof(name));
        var normalizedPhone = NormalizeOptional(phone, 50, nameof(phone));
        var normalizedEmail = NormalizeOptional(email, 254, nameof(email));
        var normalizedAddress = NormalizeOptional(address, 500, nameof(address));
        var normalizedNotes = NormalizeOptional(notes, 2000, nameof(notes));
        if (normalizedEmail is not null &&
            (!MailAddress.TryCreate(normalizedEmail, out var parsed) || parsed.Address != normalizedEmail))
            throw new ArgumentException("A valid supplier email is required.", nameof(email));

        Name = normalizedName;
        Phone = normalizedPhone;
        Email = normalizedEmail;
        Address = normalizedAddress;
        Notes = normalizedNotes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        var normalized = NormalizeOptional(value, maxLength, parameterName);
        return normalized ?? throw new ArgumentException("Supplier name is required.", parameterName);
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }
}
