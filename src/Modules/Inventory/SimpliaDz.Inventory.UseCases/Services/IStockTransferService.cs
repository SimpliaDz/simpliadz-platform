namespace SimpliaDz.Inventory.UseCases.Services;

public interface IStockTransferService
{
    Task<StockTransferDto> CreateDraftAsync(Guid sourceLocationId, Guid destinationLocationId,
        string createdBy, CancellationToken ct);
    Task<StockTransferDto> AddLineAsync(Guid transferId, Guid productVariantId, int quantity,
        CancellationToken ct);
    Task<StockTransferDto> CompleteAsync(Guid transferId, string completedBy, CancellationToken ct);
    Task CancelAsync(Guid transferId, CancellationToken ct);
    Task<StockTransferDto> GetByIdAsync(Guid transferId, CancellationToken ct);
    Task<IReadOnlyList<StockTransferDto>> ListAsync(int page, int pageSize, CancellationToken ct);
}

public sealed record StockTransferLineDto(Guid Id, Guid ProductVariantId, int Quantity);

public sealed record StockTransferDto(Guid Id, string TransferNumber, Guid SourceLocationId,
    Guid DestinationLocationId, string Status, DateTimeOffset CreatedAt, string CreatedBy,
    DateTimeOffset? CompletedAt, string? CompletedBy, IReadOnlyList<StockTransferLineDto> Lines);
