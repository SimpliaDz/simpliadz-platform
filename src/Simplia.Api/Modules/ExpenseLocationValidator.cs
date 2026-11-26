using Microsoft.EntityFrameworkCore;
using Simplia.Accounting.UseCases.Services;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Infrastructure.Data;

namespace Simplia.Api.Modules;

internal sealed class ExpenseLocationValidator(InventoryDbContext inventory,
    ITenantContext tenant) : IExpenseLocationValidator
{
    public Task<bool> IsActiveLocationAsync(Guid locationId, CancellationToken ct) =>
        tenant.IsModuleEnabled("Inventory") && locationId != Guid.Empty
            ? inventory.Locations.AsNoTracking()
                .AnyAsync(x => x.Id == locationId && x.IsActive, ct)
            : Task.FromResult(false);
}
