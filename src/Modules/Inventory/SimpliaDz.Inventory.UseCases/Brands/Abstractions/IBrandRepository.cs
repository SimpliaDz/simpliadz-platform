using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Brands.Abstractions;

public interface IBrandRepository
{
    Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken ct);
    Task<bool> ExistsByNameAsync(string name, Guid? excludingId, CancellationToken ct);
    Task AddAsync(Brand brand, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
