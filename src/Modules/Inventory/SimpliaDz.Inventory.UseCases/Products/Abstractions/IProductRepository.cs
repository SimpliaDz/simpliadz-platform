using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Products.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Product>> SearchAsync(string? search, bool? isActive, CancellationToken ct);

    Task AddAsync(Product product, CancellationToken ct);

    Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct);

    Task<bool> IsValidBrandAsync(Guid brandId, CancellationToken ct);

    Task<bool> IsValidCategoryAsync(Guid categoryId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
