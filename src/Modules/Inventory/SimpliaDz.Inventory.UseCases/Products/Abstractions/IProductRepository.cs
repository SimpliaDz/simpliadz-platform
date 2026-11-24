using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Products.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ProductVariant?> GetVariantByIdAsync(Guid id, CancellationToken ct);
    Task<ProductVariant?> GetVariantBySkuAsync(string sku, CancellationToken ct);
    Task<ProductVariant?> GetVariantByBarcodeAsync(string barcode, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct);
    Task<bool> BarcodeExistsAsync(string barcode, CancellationToken ct);
    Task<bool> TryAssignGeneratedBarcodeAsync(Guid variantId, string barcode, CancellationToken ct);
    Task<bool> TryUpdateVariantIdentifiersAsync(Guid variantId, string currentSku,
        string? currentBarcode, string sku, string? barcode, CancellationToken ct);
    Task<IReadOnlyList<AttributeValue>> GetAttributeValuesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<ProductAttribute>> GetAttributesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProductAttribute>>([]);

    Task<IReadOnlyList<Product>> SearchAsync(string? search, bool? isActive, CancellationToken ct);

    Task AddAsync(Product product, CancellationToken ct);
    Task AddVariantAsync(ProductVariant variant, CancellationToken ct);

    Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct);

    Task<bool> IsValidBrandAsync(Guid brandId, CancellationToken ct);

    Task<bool> IsValidCategoryAsync(Guid categoryId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
