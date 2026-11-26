using Microsoft.EntityFrameworkCore;
using Simplia.Accounting.Core;
using Simplia.Accounting.Infrastructure.Data;
using Simplia.Accounting.UseCases.Services;
using Simplia.BuildingBlocks.Exceptions;

namespace Simplia.Accounting.Infrastructure.Services;

public sealed class ExpenseService(AccountingDbContext db,
    IExpenseLocationValidator locations) : IExpenseService
{
    public async Task<ExpenseDto> CreateAsync(Guid expenseCategoryId, Guid? locationId,
        decimal amount, DateOnly expenseDate, string? description, Guid createdBy,
        CancellationToken ct)
    {
        var expense = new Expense(expenseCategoryId, locationId, amount,
            expenseDate, description, createdBy);
        await RequireActiveCategoryAsync(expenseCategoryId, ct);
        if (locationId.HasValue &&
            !await locations.IsActiveLocationAsync(locationId.Value, ct))
            throw new NotFoundException("Active location", locationId.Value);
        db.Expenses.Add(expense);
        await SaveAsync(ct);
        return Map(expense);
    }

    public async Task<ExpenseDto> UpdateAsync(Guid id, Guid expenseCategoryId,
        Guid? locationId, decimal amount, DateOnly expenseDate,
        string? description, CancellationToken ct)
    {
        var expense = await LoadAsync(id, ct);
        if (expenseCategoryId != expense.ExpenseCategoryId)
            await RequireActiveCategoryAsync(expenseCategoryId, ct);
        if (locationId.HasValue && locationId != expense.LocationId &&
            !await locations.IsActiveLocationAsync(locationId.Value, ct))
            throw new NotFoundException("Active location", locationId.Value);
        expense.Update(expenseCategoryId, locationId, amount, expenseDate, description);
        await SaveAsync(ct);
        return Map(expense);
    }

    public async Task<ExpenseDto> GetByIdAsync(Guid id, CancellationToken ct) =>
        Map(await db.Expenses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Expense", id));

    public async Task<IReadOnlyList<ExpenseDto>> ListAsync(DateOnly? fromDate,
        DateOnly? toDate, Guid? expenseCategoryId, Guid? locationId, int page,
        int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ArgumentException("From date cannot be after to date.", nameof(fromDate));
        if (expenseCategoryId == Guid.Empty || locationId == Guid.Empty)
            throw new ArgumentException("Filter IDs must be valid.");
        var query = db.Expenses.AsNoTracking().AsQueryable();
        if (fromDate.HasValue) query = query.Where(x => x.ExpenseDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.ExpenseDate <= toDate.Value);
        if (expenseCategoryId.HasValue)
            query = query.Where(x => x.ExpenseCategoryId == expenseCategoryId.Value);
        if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId.Value);
        return await query.OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ExpenseDto(x.Id, x.ExpenseCategoryId, x.LocationId,
                x.Amount, x.ExpenseDate, x.Description, x.CreatedBy,
                x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
    }

    private async Task RequireActiveCategoryAsync(Guid id, CancellationToken ct)
    {
        if (!await db.ExpenseCategories.AsNoTracking()
                .AnyAsync(x => x.Id == id && x.IsActive, ct))
            throw new NotFoundException("Active expense category", id);
    }

    private async Task<Expense> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Expense", id);

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("Expense changed concurrently; reload and retry.");
        }
    }

    private static ExpenseDto Map(Expense x) => new(x.Id, x.ExpenseCategoryId,
        x.LocationId, x.Amount, x.ExpenseDate, x.Description, x.CreatedBy,
        x.CreatedAt, x.UpdatedAt);
}
