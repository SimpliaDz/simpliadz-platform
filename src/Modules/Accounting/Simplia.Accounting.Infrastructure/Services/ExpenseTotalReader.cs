using Microsoft.EntityFrameworkCore;
using Simplia.Accounting.Infrastructure.Data;
using Simplia.Accounting.UseCases.Services;

namespace Simplia.Accounting.Infrastructure.Services;

public sealed class ExpenseTotalReader(AccountingDbContext db) : IExpenseTotalReader
{
    public async Task<decimal> GetTotalForDateAsync(DateOnly date, CancellationToken ct) =>
        await db.Expenses.AsNoTracking().Where(x => x.ExpenseDate == date)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
}
