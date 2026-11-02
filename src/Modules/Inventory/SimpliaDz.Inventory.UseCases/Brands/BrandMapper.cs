using SimpliaDz.Inventory.Contracts.Brands;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.UseCases.Brands;

internal static class BrandMapper
{
    public static BrandResponse ToResponse(this Brand brand) =>
        new(brand.Id, brand.Name, brand.IsActive, brand.UpdatedAt);
}
