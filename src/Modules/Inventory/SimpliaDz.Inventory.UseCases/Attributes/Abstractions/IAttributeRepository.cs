using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Attributes.Abstractions;

public interface IAttributeRepository
{
    Task<ProductAttribute?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProductAttribute>> GetAllAsync(CancellationToken ct);
    Task<bool> NameExistsAsync(string name, CancellationToken ct);
    Task AddAsync(ProductAttribute attribute, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
