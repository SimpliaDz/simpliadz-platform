using Simplia.Inventory.Core.Entities.Catalog;

namespace Simplia.Inventory.UseCases.Categories.Abstractions;

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
    Task<IReadOnlyList<ProductAttribute>> GetAttributesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProductAttribute>>([]);
    Task AddAsync(ProductCategory category, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
