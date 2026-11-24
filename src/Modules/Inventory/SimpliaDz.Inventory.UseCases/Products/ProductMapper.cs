using SimpliaDz.Inventory.Contracts.Products;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Products;

internal static class ProductMapper
{
    public static ProductResponse ToResponse(this Product product) =>
        new(
            product.Id,
            product.Name.Value,
            product.Code,
            product.Description,
            product.BrandId,
            product.ProductCategoryId,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt,
            product.Variants
                .OrderBy(variant => variant.Sku.Value)
                .Select(variant => new ProductVariantResponse(
                    variant.Id,
                    variant.Sku.Value,
                    variant.Barcode,
                    variant.Color,
                    variant.Size,
                    variant.RetailPrice,
                    variant.WholesalePrice,
                    variant.IsActive))
                .ToArray());
}
