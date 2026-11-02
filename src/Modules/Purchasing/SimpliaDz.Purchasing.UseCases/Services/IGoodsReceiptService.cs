namespace SimpliaDz.Purchasing.UseCases.Services;

public sealed record ReceiveGoodsLine(Guid PurchaseOrderLineId, int QuantityReceived);

public sealed record GoodsReceiptLineDto(Guid Id, Guid PurchaseOrderLineId, Guid ProductVariantId,
    int QuantityReceived, decimal UnitCost);

public sealed record GoodsReceiptDto(Guid Id, Guid PurchaseOrderId, Guid LocationId,
    DateTimeOffset ReceivedAt, string ReceivedBy, IReadOnlyList<GoodsReceiptLineDto> Lines);

public interface IGoodsReceiptService
{
    Task<GoodsReceiptDto> ReceiveAsync(Guid receiptId, Guid purchaseOrderId,
        IReadOnlyList<ReceiveGoodsLine> lines, string receivedBy, CancellationToken ct);
    Task<GoodsReceiptDto> GetByIdAsync(Guid receiptId, CancellationToken ct);
    Task<IReadOnlyList<GoodsReceiptDto>> ListByPurchaseOrderAsync(Guid purchaseOrderId,
        CancellationToken ct);
}
