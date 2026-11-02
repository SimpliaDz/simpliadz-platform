using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Purchasing.UseCases.Services;

namespace SimpliaDz.Api.Modules;

internal sealed class InventoryReferenceValidator(InventoryDbContext db, ITenantContext tenant)
    : IInventoryReferenceValidator
{
    public Task<bool> IsActiveLocationAsync(Guid locationId, CancellationToken ct) =>
        tenant.IsModuleEnabled("Inventory") && locationId != Guid.Empty
            ? db.Locations.AsNoTracking().AnyAsync(x => x.Id == locationId && x.IsActive, ct)
            : Task.FromResult(false);

    public Task<bool> IsActiveVariantAsync(Guid variantId, CancellationToken ct) =>
        tenant.IsModuleEnabled("Inventory") && variantId != Guid.Empty
            ? db.ProductVariants.AsNoTracking().AnyAsync(x => x.Id == variantId && x.IsActive && x.Product.IsActive, ct)
            : Task.FromResult(false);
}
