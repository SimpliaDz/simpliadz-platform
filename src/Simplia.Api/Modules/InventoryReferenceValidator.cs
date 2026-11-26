using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Purchasing.UseCases.Services;

namespace Simplia.Api.Modules;

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
