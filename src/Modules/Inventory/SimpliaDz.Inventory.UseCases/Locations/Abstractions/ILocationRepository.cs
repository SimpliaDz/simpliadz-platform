using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.UseCases.Locations.Abstractions;

public interface ILocationRepository
{
    Task<InventoryLocation?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<InventoryLocation>> GetAllAsync(CancellationToken ct);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct);
    Task<bool> IsActiveAsync(Guid id, CancellationToken ct);
    Task AddAsync(InventoryLocation location, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
