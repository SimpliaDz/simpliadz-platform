namespace SimpliaDz.Sales.UseCases.Services;

public sealed record PosRegisterDto(Guid Id, Guid LocationId, string Code, string Name,
    Guid? CashSessionId, Guid? OccupiedByUserId, bool IsAvailable);

public sealed record PosContextDto(bool IsClockedIn, Guid? WorkSessionId,
    Guid? CashRegisterId, string? CashRegisterCode, string? CashRegisterName,
    Guid? CashSessionId, Guid? DefaultPriceListId, string? CurrencyCode,
    IReadOnlyList<PosRegisterDto> Registers);

public interface IPosService
{
    Task<PosContextDto> GetContextAsync(Guid userId, CancellationToken ct);
    Task<PosContextDto> AssignAsync(Guid userId, Guid registerId, CancellationToken ct);
    Task ReleaseAsync(Guid userId, CancellationToken ct);
    Task<(Guid LocationId, Guid CashSessionId, Guid PriceListId)> RequireContextAsync(
        Guid userId, CancellationToken ct);
}
