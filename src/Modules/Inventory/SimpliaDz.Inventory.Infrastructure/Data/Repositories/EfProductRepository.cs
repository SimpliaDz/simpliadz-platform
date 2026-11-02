using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SimpliaDz.BuildingBlocks.Exceptions;
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

    public async Task<IReadOnlyList<Product>> SearchAsync(
        string? search,
        bool? isActive,
        CancellationToken ct)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(product => product.Variants)
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(product => product.IsActive == isActive.Value);

        var products = await query.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            products = products
                .Where(product =>
                    product.Name.Value.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (product.Code?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (product.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        return products
            .OrderBy(product => product.Name.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task AddAsync(Product product, CancellationToken ct)
        => await _db.Products.AddAsync(product, ct);

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct)
        => _db.Products.AnyAsync(
            product => product.Code == code && (!excludingId.HasValue || product.Id != excludingId.Value),
            ct);

    public Task<bool> IsValidBrandAsync(Guid brandId, CancellationToken ct)
        => _db.Brands.AnyAsync(brand => brand.Id == brandId && brand.IsActive, ct);

    public Task<bool> IsValidCategoryAsync(Guid categoryId, CancellationToken ct)
        => _db.Categories.AnyAsync(category => category.Id == categoryId && category.IsActive, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A product with the same code already exists.");
        }
    }
}
