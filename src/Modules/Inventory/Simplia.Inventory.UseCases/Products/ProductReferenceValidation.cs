using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.UseCases.Products.Abstractions;

namespace Simplia.Inventory.UseCases.Products;

internal static class ProductReferenceValidation
{
    public static async Task EnsureValidAsync(
        IProductRepository products,
        Guid? brandId,
        Guid? categoryId,
        CancellationToken ct)
    {
        if (brandId.HasValue && !await products.IsValidBrandAsync(brandId.Value, ct))
            throw new NotFoundException("Active brand", brandId.Value);

        if (categoryId.HasValue && !await products.IsValidCategoryAsync(categoryId.Value, ct))
            throw new NotFoundException("Active product category", categoryId.Value);
    }
}
