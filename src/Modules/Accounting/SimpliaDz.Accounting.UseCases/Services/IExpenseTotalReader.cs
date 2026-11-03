namespace SimpliaDz.Accounting.UseCases.Services;

public interface IExpenseTotalReader
{
    Task<decimal> GetTotalForDateAsync(DateOnly date, CancellationToken ct);
}
