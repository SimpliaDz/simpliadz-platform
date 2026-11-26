namespace Simplia.Sales.UseCases.Services;

public sealed record CashRegisterDto(Guid Id, Guid LocationId, string Code,
    string Name, bool IsActive);

public interface ICashRegisterService
{
    Task<CashRegisterDto> CreateAsync(Guid locationId, string code, string name,
        CancellationToken ct);
    Task<CashRegisterDto> UpdateAsync(Guid id, string code, string name,
        CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CashRegisterDto>> ListAsync(Guid? locationId, CancellationToken ct);
}
