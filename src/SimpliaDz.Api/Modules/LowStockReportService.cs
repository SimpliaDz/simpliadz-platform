using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Reporting.UseCases.LowStock;

namespace SimpliaDz.Api.Modules;

public sealed class LowStockReportService(InventoryDbContext inventory, ITenantContext tenant)
    : ILowStockReportService
{
    public async Task<LowStockReportDto> GetAsync(Guid? locationId, int page,
        int pageSize, CancellationToken ct)
    {
        if (locationId == Guid.Empty)
            throw new ArgumentException("Location ID must be valid.", nameof(locationId));
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (!tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Inventory must be enabled for low stock reports.");
        if (locationId.HasValue && !await inventory.Locations.AsNoTracking()
                .AnyAsync(x => x.Id == locationId.Value, ct))
            throw new NotFoundException("Location", locationId.Value);

        var query = from policy in inventory.InventoryPolicies.AsNoTracking()
                    join balance in inventory.StockBalances.AsNoTracking()
                        on new { VariantId = policy.ProductVariantId, policy.LocationId }
                        equals new { VariantId = balance.VariantId, balance.LocationId }
                        into balances
                    from balance in balances.DefaultIfEmpty()
                    where policy.ProductVariant.IsActive &&
                          policy.ProductVariant.Product.IsActive &&
                          policy.Location.IsActive
                    select new
                    {
                        policy,
                        quantity = balance == null ? 0 : balance.QuantityOnHand
                    };
        if (locationId.HasValue)
            query = query.Where(x => x.policy.LocationId == locationId.Value);
        query = query.Where(x => x.quantity <= x.policy.ReorderPoint);

        var total = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(x => x.policy.Location.Name)
            .ThenBy(x => x.policy.ProductVariant.Product.Name)
            .ThenBy(x => x.policy.ProductVariant.Sku)
            .ThenBy(x => x.policy.ProductVariantId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new LowStockReportRow(x.policy.ProductVariantId,
                x.policy.ProductVariant.ProductId, x.policy.ProductVariant.Product.Name,
                x.policy.ProductVariant.Sku, x.policy.LocationId,
                x.policy.Location.Name, x.quantity, x.policy.ReorderPoint,
                x.quantity == 0))
            .ToListAsync(ct);
        return new LowStockReportDto(locationId, total, rows);
    }
}
