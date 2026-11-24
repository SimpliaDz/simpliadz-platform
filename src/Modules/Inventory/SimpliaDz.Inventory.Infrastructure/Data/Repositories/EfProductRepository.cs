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
            .Include(p => p.ProductCategory).ThenInclude(category => category!.Attributes)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    private IQueryable<ProductVariant> VariantsWithValues => _db.ProductVariants
        .Include(v => v.AttributeValues).ThenInclude(link => link.AttributeValue)
            .ThenInclude(value => value.Attribute);

    public Task<ProductVariant?> GetVariantByIdAsync(Guid id, CancellationToken ct) =>
        VariantsWithValues.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<ProductVariant?> GetVariantBySkuAsync(string sku, CancellationToken ct) =>
        VariantsWithValues.FirstOrDefaultAsync(v => v.Sku == new Core.ValueObjects.Sku(sku), ct);

    public Task<ProductVariant?> GetVariantByBarcodeAsync(string barcode, CancellationToken ct) =>
        VariantsWithValues.FirstOrDefaultAsync(v => v.Barcode == barcode, ct);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct) =>
        _db.ProductVariants.AnyAsync(v => v.Sku == new Core.ValueObjects.Sku(sku), ct);

    public Task<bool> BarcodeExistsAsync(string barcode, CancellationToken ct) =>
        _db.ProductVariants.AnyAsync(v => v.Barcode == barcode, ct);

    public async Task<bool> TryAssignGeneratedBarcodeAsync(Guid variantId, string barcode,
        CancellationToken ct)
    {
        try
        {
            return await _db.ProductVariants.Where(v => v.Id == variantId &&
                    v.Barcode == null && v.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.Barcode, barcode), ct) == 1;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ConflictException("Generated barcode conflicts with an existing barcode.");
        }
    }

    public async Task<bool> TryUpdateVariantIdentifiersAsync(Guid variantId, string currentSku,
        string? currentBarcode, string sku, string? barcode, CancellationToken ct)
    {
        try
        {
            return await _db.ProductVariants.Where(v => v.Id == variantId && v.IsActive &&
                    v.Sku == new Core.ValueObjects.Sku(currentSku) && v.Barcode == currentBarcode)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.Sku, new Core.ValueObjects.Sku(sku))
                    .SetProperty(v => v.Barcode, barcode), ct) == 1;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ConflictException("Variant SKU or barcode already exists.");
        }
    }

    public async Task<IReadOnlyList<AttributeValue>> GetAttributeValuesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await _db.AttributeValues.AsNoTracking().Where(v => ids.Contains(v.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductAttribute>> GetAttributesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await _db.ProductAttributes.AsNoTracking().Where(attribute => ids.Contains(attribute.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> SearchAsync(
        string? search,
        bool? isActive,
        CancellationToken ct)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(product => product.ProductCategory).ThenInclude(category => category!.Attributes)
            .Include(product => product.Variants).ThenInclude(v => v.AttributeValues)
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

    public async Task AddVariantAsync(ProductVariant variant, CancellationToken ct)
        => await _db.ProductVariants.AddAsync(variant, ct);

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
            throw new ConflictException("A product code, variant SKU, barcode, or attribute combination already exists.");
        }
    }
}
