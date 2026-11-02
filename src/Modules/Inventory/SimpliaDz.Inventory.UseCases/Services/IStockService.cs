namespace SimpliaDz.Inventory.UseCases.Services;

public interface IStockService
{
    Task<IReadOnlyList<StockBalanceDto>> GetByVariantAsync(Guid variantId, CancellationToken ct);
    Task<IReadOnlyList<StockBalanceDto>> GetByLocationAsync(Guid locationId, CancellationToken ct);
    Task<int> GetTotalByVariantAsync(Guid variantId, CancellationToken ct);

    Task ReceiveAsync(
        Guid variantId,
        Guid locationId,
        int quantity,
        string? reference,
        string? note,
        CancellationToken ct);

    Task AdjustToAsync(
        Guid variantId,
        Guid locationId,
        int newQuantity,
        string? note,
        CancellationToken ct);
}

public sealed record StockBalanceDto(Guid ProductVariantId, Guid LocationId, int QuantityOnHand, DateTimeOffset UpdatedAt);
