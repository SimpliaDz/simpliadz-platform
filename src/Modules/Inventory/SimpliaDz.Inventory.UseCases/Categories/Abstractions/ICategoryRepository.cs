using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Categories.Abstractions;

public interface ICategoryRepository
{
    Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProductCategory>> GetAllAsync(CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsByNameAsync(
        string name,
        Guid? parentCategoryId,
        Guid? excludingId,
        CancellationToken ct);
    Task<bool> WouldCreateCycleAsync(Guid categoryId, Guid parentCategoryId, CancellationToken ct);
    Task AddAsync(ProductCategory category, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
