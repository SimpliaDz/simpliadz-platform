using Microsoft.EntityFrameworkCore;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;

namespace SimpliaDz.Inventory.Infrastructure.Data.Repositories;

internal sealed class EfProductRepository : IProductRepository
{
    private readonly InventoryDbContext _db;

    public EfProductRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Product product, CancellationToken ct)
        => await _db.Products.AddAsync(product, ct);

    public Task UpdateAsync(Product product, CancellationToken ct)
    {
        _db.Products.Update(product);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Product product, CancellationToken ct)
    {
        _db.Products.Remove(product);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct)
        => await _db.Products.AnyAsync(p => p.Code == code, ct);

    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);
}
