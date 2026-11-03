namespace SimpliaDz.Accounting.UseCases.Services;

public interface IExpenseLocationValidator
{
    Task<bool> IsActiveLocationAsync(Guid locationId, CancellationToken ct);
}

public sealed record ExpenseDto(Guid Id, Guid ExpenseCategoryId, Guid? LocationId,
    decimal Amount, DateOnly ExpenseDate, string? Description, Guid CreatedBy,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public interface IExpenseService
{
    Task<ExpenseDto> CreateAsync(Guid expenseCategoryId, Guid? locationId,
        decimal amount, DateOnly expenseDate, string? description, Guid createdBy,
        CancellationToken ct);
    Task<ExpenseDto> UpdateAsync(Guid id, Guid expenseCategoryId, Guid? locationId,
        decimal amount, DateOnly expenseDate, string? description, CancellationToken ct);
    Task<ExpenseDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ExpenseDto>> ListAsync(DateOnly? fromDate, DateOnly? toDate,
        Guid? expenseCategoryId, Guid? locationId, int page, int pageSize,
        CancellationToken ct);
}
