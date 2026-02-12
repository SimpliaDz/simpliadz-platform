namespace SimpliaDz.Inventory.UseCases.Services;

public interface IStockService
{
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
