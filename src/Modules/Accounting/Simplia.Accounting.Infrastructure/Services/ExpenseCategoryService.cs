using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.Accounting.Core;
using Simplia.Accounting.Infrastructure.Data;
using Simplia.Accounting.UseCases.Services;
using Simplia.BuildingBlocks.Exceptions;

namespace Simplia.Accounting.Infrastructure.Services;

internal sealed class ExpenseCategoryService(AccountingDbContext db) : IExpenseCategoryService
{
    public async Task<ExpenseCategoryDto> CreateAsync(string name, CancellationToken ct)
    {
        var category = new ExpenseCategory(name);
        if (await db.ExpenseCategories.AnyAsync(x => x.Name == category.Name, ct))
            throw new ConflictException("Expense category name already exists.");
        db.ExpenseCategories.Add(category);
        await SaveAsync(ct);
        return Map(category);
    }

    public async Task<ExpenseCategoryDto> UpdateAsync(Guid id, string name, CancellationToken ct)
    {
        var category = await LoadAsync(id, ct);
        var normalizedName = new ExpenseCategory(name).Name;
        if (await db.ExpenseCategories.AnyAsync(x => x.Id != id && x.Name == normalizedName, ct))
            throw new ConflictException("Expense category name already exists.");
        category.Update(normalizedName);
        await SaveAsync(ct);
        return Map(category);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var category = await LoadAsync(id, ct);
        category.Deactivate();
        await SaveAsync(ct);
    }

    public async Task<ExpenseCategoryDto> GetByIdAsync(Guid id, CancellationToken ct) =>
        Map(await LoadAsync(id, ct));

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListAsync(bool? isActive,
        CancellationToken ct)
    {
        var query = db.ExpenseCategories.AsNoTracking().AsQueryable();
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new ExpenseCategoryDto(x.Id, x.Name, x.IsActive,
                x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
    }

    private async Task<ExpenseCategory> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Expense category", id);

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Expense category changed concurrently; reload and retry.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Expense category name already exists.");
        }
    }

    private static ExpenseCategoryDto Map(ExpenseCategory x) =>
        new(x.Id, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt);
}
