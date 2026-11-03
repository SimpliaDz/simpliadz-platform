using Microsoft.EntityFrameworkCore;
using SimpliaDz.Accounting.Infrastructure.Data;
using SimpliaDz.Accounting.UseCases.Services;

namespace SimpliaDz.Accounting.Infrastructure.Services;

public sealed class ExpenseTotalReader(AccountingDbContext db) : IExpenseTotalReader
{
    public async Task<decimal> GetTotalForDateAsync(DateOnly date, CancellationToken ct) =>
        await db.Expenses.AsNoTracking().Where(x => x.ExpenseDate == date)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
}
