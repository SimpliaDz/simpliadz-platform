using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Products.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);

    Task AddAsync(Product product, CancellationToken ct);

    Task UpdateAsync(Product product, CancellationToken ct);

    Task DeleteAsync(Product product, CancellationToken ct);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
