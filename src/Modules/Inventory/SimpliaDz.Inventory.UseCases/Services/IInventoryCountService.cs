namespace SimpliaDz.Inventory.UseCases.Services;

public interface IInventoryCountService
{
    Task<InventoryCountDto> StartAsync(Guid locationId, string createdBy, CancellationToken ct);
    Task<InventoryCountDto> AddVariantAsync(Guid countId, Guid productVariantId, CancellationToken ct);
    Task<InventoryCountDto> SetCountedQuantityAsync(Guid countId, Guid productVariantId,
        int countedQuantity, CancellationToken ct);
    Task<InventoryCountDto> CompleteAsync(Guid countId, string completedBy, CancellationToken ct);
    Task CancelAsync(Guid countId, CancellationToken ct);
    Task<InventoryCountDto> GetByIdAsync(Guid countId, CancellationToken ct);
    Task<IReadOnlyList<InventoryCountDto>> ListAsync(Guid? locationId, int page, int pageSize,
        CancellationToken ct);
}

public sealed record InventoryCountLineDto(Guid Id, Guid ProductVariantId, int ExpectedQuantity,
    int? CountedQuantity, int? Difference);

public sealed record InventoryCountDto(Guid Id, Guid LocationId, string Status,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string CreatedBy,
    string? CompletedBy, IReadOnlyList<InventoryCountLineDto> Lines);
