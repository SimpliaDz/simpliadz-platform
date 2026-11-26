using Simplia.Inventory.Contracts.Locations;
using Simplia.Inventory.Core.Entities.Stock;

namespace Simplia.Inventory.UseCases.Locations;

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
