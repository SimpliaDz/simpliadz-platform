namespace Simplia.Sales.UseCases.Services;

public sealed record CashSessionDto(Guid Id, Guid CashRegisterId, Guid LocationId,
    string Status, Guid OpenedBy, DateTimeOffset OpenedAt, decimal OpeningCash,
    decimal CashPayments, decimal OtherPayments, decimal CashMovements,
    decimal ExpectedCash, decimal? CountedCash, decimal? Difference,
    Guid? ClosedBy, DateTimeOffset? ClosedAt);

public sealed record CashMovementDto(Guid Id, Guid CashSessionId, decimal AmountDelta,
    string Reason, Guid CreatedBy, DateTimeOffset OccurredAt);

public interface ICashSessionService
{
    Task<CashSessionDto> OpenAsync(Guid registerId, decimal openingCash,
        Guid openedBy, CancellationToken ct);
    Task<CashSessionDto> CloseAsync(Guid id, decimal countedCash,
        Guid closedBy, CancellationToken ct);
    Task<CashSessionDto> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CashSessionDto>> GetCurrentAsync(Guid locationId,
        CancellationToken ct);
    Task<IReadOnlyList<CashSessionDto>> ListAsync(Guid? locationId,
        int page, int pageSize, CancellationToken ct);
    Task<CashMovementDto> AddMovementAsync(Guid id, decimal amountDelta,
        string reason, Guid createdBy, CancellationToken ct);
    Task<IReadOnlyList<CashMovementDto>> GetMovementsAsync(Guid id,
        CancellationToken ct);
}
