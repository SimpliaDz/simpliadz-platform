namespace Simplia.Accounting.Core;

public sealed class Expense
{
    private const decimal MaxAmount = 99999999999999.9999m;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ExpenseCategoryId { get; private set; }
    public ExpenseCategory ExpenseCategory { get; private set; } = default!;
    public Guid? LocationId { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly ExpenseDate { get; private set; }
    public string? Description { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; private set; } = [];

    private Expense() { }

    public Expense(Guid expenseCategoryId, Guid? locationId, decimal amount,
        DateOnly expenseDate, string? description, Guid createdBy)
    {
        if (createdBy == Guid.Empty)
            throw new ArgumentException("Creator is required.", nameof(createdBy));
        CreatedBy = createdBy;
        Update(expenseCategoryId, locationId, amount, expenseDate, description);
    }

    public void Update(Guid expenseCategoryId, Guid? locationId, decimal amount,
        DateOnly expenseDate, string? description)
    {
        if (expenseCategoryId == Guid.Empty)
            throw new ArgumentException("Expense category is required.", nameof(expenseCategoryId));
        if (locationId == Guid.Empty)
            throw new ArgumentException("Location ID must be valid when supplied.", nameof(locationId));
        if (amount <= 0 || amount > MaxAmount || decimal.Round(amount, 4) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Amount must be positive and fit decimal(18,4).");
        if (expenseDate == default)
            throw new ArgumentException("Expense date is required.", nameof(expenseDate));
        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null : description.Trim();
        if (normalizedDescription?.Length > 2000)
            throw new ArgumentException("Description cannot exceed 2000 characters.", nameof(description));

        ExpenseCategoryId = expenseCategoryId;
        LocationId = locationId;
        Amount = amount;
        ExpenseDate = expenseDate;
        Description = normalizedDescription;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
