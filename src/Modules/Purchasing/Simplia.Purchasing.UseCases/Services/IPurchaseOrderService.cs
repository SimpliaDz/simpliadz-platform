namespace Simplia.Purchasing.UseCases.Services;

public interface IInventoryReferenceValidator
{
    Task<bool> IsActiveLocationAsync(Guid locationId, CancellationToken ct);
    Task<bool> IsActiveVariantAsync(Guid variantId, CancellationToken ct);
}

public interface IPurchaseOrderService
{
    Task<PurchaseOrderDto> CreateDraftAsync(Guid supplierId, Guid destinationLocationId,
        string? notes, string createdBy, CancellationToken ct);
    Task<PurchaseOrderDto> AddLineAsync(Guid orderId, Guid productVariantId,
        int orderedQuantity, decimal unitCost, CancellationToken ct);
    Task<PurchaseOrderDto> UpdateNotesAsync(Guid orderId, string? notes, CancellationToken ct);
    Task<PurchaseOrderDto> PlaceOrderAsync(Guid orderId, CancellationToken ct);
    Task CancelAsync(Guid orderId, CancellationToken ct);
    Task<PurchaseOrderDto> GetByIdAsync(Guid orderId, CancellationToken ct);
    Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(Guid? supplierId, string? status,
        int page, int pageSize, CancellationToken ct);
}

public sealed record PurchaseOrderLineDto(Guid Id, Guid ProductVariantId,
    int OrderedQuantity, int ReceivedQuantity, decimal UnitCost, decimal LineTotal);

public sealed record PurchaseOrderDto(Guid Id, string PurchaseOrderNumber, Guid SupplierId,
    Guid DestinationLocationId, string Status, DateTimeOffset OrderDate, string? Notes,
    string CreatedBy, decimal Total, IReadOnlyList<PurchaseOrderLineDto> Lines);
