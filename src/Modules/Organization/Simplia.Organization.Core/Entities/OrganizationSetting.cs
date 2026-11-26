namespace Simplia.Organization.Core.Entities;

public sealed class OrganizationSetting
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string ValueType { get; private set; } = "String";
    public bool IsSecret { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid UpdatedBy { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private OrganizationSetting() { }
    public OrganizationSetting(string key, string value, string valueType, bool isSecret, Guid updatedBy) => Update(key, value, valueType, isSecret, updatedBy);
    public void Update(string key, string value, string valueType, bool isSecret, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 150) throw new ArgumentException("Setting key is required and must fit 150 characters.", nameof(key));
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4000) throw new ArgumentException("Setting value is required and must fit 4000 characters.", nameof(value));
        if (updatedBy == Guid.Empty) throw new ArgumentException("Updating user is required.", nameof(updatedBy));
        Key = key.Trim(); Value = value; ValueType = valueType; IsSecret = isSecret; UpdatedBy = updatedBy; UpdatedAt = DateTimeOffset.UtcNow;
    }
}
