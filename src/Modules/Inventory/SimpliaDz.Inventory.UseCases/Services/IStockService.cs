namespace SimpliaDz.Inventory.UseCases.Services;

public interface IStockService
{
    Task<IReadOnlyList<StockBalanceDto>> GetByVariantAsync(Guid variantId, CancellationToken ct);
    Task<IReadOnlyList<StockBalanceDto>> GetByLocationAsync(Guid locationId, CancellationToken ct);
    Task<int> GetTotalByVariantAsync(Guid variantId, CancellationToken ct);
    Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(Guid? variantId, Guid? locationId,
        int page, int pageSize, CancellationToken ct);

    Task<OpeningStockResult> OpenAsync(Guid variantId, Guid locationId, int quantity,
        string? reason, string createdBy, CancellationToken ct);

    Task ReceiveAsync(
        Guid variantId,
        Guid locationId,
        int quantity,
        string? reference,
        string? reason,
        string? referenceType,
        Guid? referenceId,
        string createdBy,
        CancellationToken ct);

    Task AdjustToAsync(
        Guid variantId,
        Guid locationId,
        int newQuantity,
        string reason,
        string createdBy,
        CancellationToken ct);

    Task AdjustByAsync(
        Guid variantId,
        Guid locationId,
        int quantityDelta,
        string reason,
        string createdBy,
        CancellationToken ct);
}

public sealed record StockBalanceDto(Guid ProductVariantId, Guid LocationId, int QuantityOnHand, DateTimeOffset UpdatedAt);
public sealed record StockMovementDto(Guid Id, Guid ProductVariantId, Guid LocationId, int QuantityDelta,
    string MovementType, string? Reference, string? ReferenceType, Guid? ReferenceId,
    string? Reason, DateTimeOffset OccurredAt, string CreatedBy);
public sealed record OpeningStockResult(Guid ProductVariantId, Guid LocationId, int QuantityOnHand,
    Guid MovementId, DateTimeOffset OccurredAt, string CreatedBy);
