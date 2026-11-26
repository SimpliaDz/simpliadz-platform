namespace Simplia.Inventory.UseCases.Services;

public sealed record InventoryPolicyDto(Guid ProductVariantId, Guid LocationId,
    int ReorderPoint, int? TargetStockLevel, int QuantityOnHand,
    bool IsBelowReorderPoint, bool IsOutOfStock, DateTimeOffset UpdatedAt);

public sealed record OutOfStockVariantDto(Guid ProductVariantId, Guid LocationId,
    int QuantityOnHand, int? ReorderPoint, int? TargetStockLevel);

public interface IInventoryPolicyService
{
    Task<InventoryPolicyDto> ConfigureAsync(Guid productVariantId, Guid locationId,
        int reorderPoint, int? targetStockLevel, CancellationToken ct);
    Task<InventoryPolicyDto> GetAsync(Guid productVariantId, Guid locationId,
        CancellationToken ct);
    Task<IReadOnlyList<InventoryPolicyDto>> ListAsync(Guid? locationId,
        bool? belowReorderPoint, bool? outOfStock, int page, int pageSize,
        CancellationToken ct);
    Task<IReadOnlyList<OutOfStockVariantDto>> GetOutOfStockByLocationAsync(
        Guid locationId, int page, int pageSize, CancellationToken ct);
}
