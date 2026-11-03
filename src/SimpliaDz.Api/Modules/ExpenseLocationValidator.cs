using Microsoft.EntityFrameworkCore;
using SimpliaDz.Accounting.UseCases.Services;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Infrastructure.Data;

namespace SimpliaDz.Api.Modules;

internal sealed class ExpenseLocationValidator(InventoryDbContext inventory,
    ITenantContext tenant) : IExpenseLocationValidator
{
    public Task<bool> IsActiveLocationAsync(Guid locationId, CancellationToken ct) =>
        tenant.IsModuleEnabled("Inventory") && locationId != Guid.Empty
            ? inventory.Locations.AsNoTracking()
                .AnyAsync(x => x.Id == locationId && x.IsActive, ct)
            : Task.FromResult(false);
}
