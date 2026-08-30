using SimpliaDz.Inventory.Contracts.Locations;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.UseCases.Locations;

internal static class LocationMapper
{
    public static LocationResponse ToResponse(this InventoryLocation location) =>
        new(
            location.Id,
            location.Name,
            location.Code,
            location.Type.ToString(),
            location.Address,
            location.Phone,
            location.IsActive,
            location.CreatedAt,
            location.UpdatedAt);
}
