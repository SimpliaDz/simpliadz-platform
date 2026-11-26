using Simplia.Inventory.Contracts.Brands;
using Simplia.Inventory.Core.Entities.Catalog;

namespace Simplia.Inventory.UseCases.Brands;

internal static class BrandMapper
{
    public static BrandResponse ToResponse(this Brand brand) =>
        new(brand.Id, brand.Name, brand.IsActive, brand.UpdatedAt);
}
