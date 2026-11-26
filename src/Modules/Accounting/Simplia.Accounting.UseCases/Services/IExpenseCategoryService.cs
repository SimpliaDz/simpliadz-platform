namespace Simplia.Accounting.UseCases.Services;

public sealed record ExpenseCategoryDto(Guid Id, string Name, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public interface IExpenseCategoryService
{
    Task<ExpenseCategoryDto> CreateAsync(string name, CancellationToken ct);
    Task<ExpenseCategoryDto> UpdateAsync(Guid id, string name, CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
    Task<ExpenseCategoryDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ExpenseCategoryDto>> ListAsync(bool? isActive,
        CancellationToken ct);
}
