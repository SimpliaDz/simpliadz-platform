using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Prices.Abstractions;

public interface IPriceListRepository
{
    Task<PriceList?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PriceList>> GetAllAsync(CancellationToken ct);
    Task<bool> ExistsByNameAsync(string name, string currencyCode, CancellationToken ct);
    Task<bool> VariantExistsAsync(Guid variantId, CancellationToken ct);
    Task AddAsync(PriceList list, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
