namespace Simplia.Accounting.Core;

public sealed class ExpenseCategory
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; private set; } = [];

    private ExpenseCategory() { }

    public ExpenseCategory(string name) => Update(name);

    public void Update(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new ArgumentException("Expense category name is required and cannot exceed 150 characters.", nameof(name));
        Name = name.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
