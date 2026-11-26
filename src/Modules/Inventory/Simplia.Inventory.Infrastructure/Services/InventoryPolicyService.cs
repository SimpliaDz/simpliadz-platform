using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Inventory.UseCases.Services;

namespace Simplia.Inventory.Infrastructure.Services;

internal sealed class InventoryPolicyService(InventoryDbContext db) : IInventoryPolicyService
{
    public async Task<InventoryPolicyDto> ConfigureAsync(Guid productVariantId,
        Guid locationId, int reorderPoint, int? targetStockLevel, CancellationToken ct)
    {
        // Validate before touching the database. Policies never create stock balances.
        _ = new InventoryPolicy(productVariantId, locationId, reorderPoint, targetStockLevel);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (!await db.Locations.AnyAsync(x => x.Id == locationId && x.IsActive, ct))
                throw new NotFoundException("Active location", locationId);
            if (!await db.ProductVariants.AnyAsync(x => x.Id == productVariantId &&
                    x.IsActive && x.Product.IsActive, ct))
                throw new NotFoundException("Active product variant", productVariantId);
            var policy = await db.InventoryPolicies.FirstOrDefaultAsync(x =>
                x.ProductVariantId == productVariantId && x.LocationId == locationId, ct);
            if (policy is null)
            {
                policy = new InventoryPolicy(productVariantId, locationId,
                    reorderPoint, targetStockLevel);
                db.InventoryPolicies.Add(policy);
            }
            else policy.Configure(reorderPoint, targetStockLevel);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var quantity = await GetQuantityAsync(productVariantId, locationId, ct);
            return Map(policy, quantity);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw new ConflictException("Inventory policy changed concurrently; reload and retry.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw new ConflictException("Inventory policy changed concurrently; reload and retry.");
        }
    }

    public async Task<InventoryPolicyDto> GetAsync(Guid productVariantId,
        Guid locationId, CancellationToken ct)
    {
        var policy = await db.InventoryPolicies.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ProductVariantId == productVariantId && x.LocationId == locationId, ct)
            ?? throw new NotFoundException("Inventory policy", $"{productVariantId}/{locationId}");
        return Map(policy, await GetQuantityAsync(productVariantId, locationId, ct));
    }

    public async Task<IReadOnlyList<InventoryPolicyDto>> ListAsync(Guid? locationId,
        bool? belowReorderPoint, bool? outOfStock, int page, int pageSize,
        CancellationToken ct)
    {
        if (locationId == Guid.Empty)
            throw new ArgumentException("Location ID must be valid.", nameof(locationId));
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));

        var query = from policy in db.InventoryPolicies.AsNoTracking()
                    join balance in db.StockBalances.AsNoTracking()
                        on new { VariantId = policy.ProductVariantId, policy.LocationId }
                        equals new { VariantId = balance.VariantId, balance.LocationId }
                        into balanceRows
                    from balance in balanceRows.DefaultIfEmpty()
                    where policy.ProductVariant.IsActive &&
                          policy.ProductVariant.Product.IsActive && policy.Location.IsActive
                    select new { policy, quantity = balance == null ? 0 : balance.QuantityOnHand };
        if (locationId.HasValue)
            query = query.Where(x => x.policy.LocationId == locationId.Value);
        if (belowReorderPoint.HasValue)
            query = belowReorderPoint.Value
                ? query.Where(x => x.quantity < x.policy.ReorderPoint)
                : query.Where(x => x.quantity >= x.policy.ReorderPoint);
        if (outOfStock.HasValue)
            query = outOfStock.Value
                ? query.Where(x => x.quantity == 0)
                : query.Where(x => x.quantity > 0);
        return await query.OrderBy(x => x.policy.LocationId)
            .ThenBy(x => x.policy.ProductVariantId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new InventoryPolicyDto(x.policy.ProductVariantId,
                x.policy.LocationId, x.policy.ReorderPoint,
                x.policy.TargetStockLevel, x.quantity,
                x.quantity < x.policy.ReorderPoint, x.quantity == 0,
                x.policy.UpdatedAt)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OutOfStockVariantDto>> GetOutOfStockByLocationAsync(
        Guid locationId, int page, int pageSize, CancellationToken ct)
    {
        if (locationId == Guid.Empty)
            throw new ArgumentException("Location is required.", nameof(locationId));
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (!await db.Locations.AnyAsync(x => x.Id == locationId && x.IsActive, ct))
            throw new NotFoundException("Active location", locationId);

        var query = from variant in db.ProductVariants.AsNoTracking()
                    where variant.IsActive && variant.Product.IsActive
                    join balance in db.StockBalances.AsNoTracking()
                        .Where(x => x.LocationId == locationId)
                        on variant.Id equals balance.VariantId into balanceRows
                    from balance in balanceRows.DefaultIfEmpty()
                    join policy in db.InventoryPolicies.AsNoTracking()
                        .Where(x => x.LocationId == locationId)
                        on variant.Id equals policy.ProductVariantId into policyRows
                    from policy in policyRows.DefaultIfEmpty()
                    where balance == null || balance.QuantityOnHand == 0
                    select new { variant.Id, policy };
        return await query.OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new OutOfStockVariantDto(x.Id, locationId, 0,
                x.policy == null ? null : x.policy.ReorderPoint,
                x.policy == null ? null : x.policy.TargetStockLevel))
            .ToListAsync(ct);
    }

    private async Task<int> GetQuantityAsync(Guid productVariantId,
        Guid locationId, CancellationToken ct) =>
        await db.StockBalances.AsNoTracking().Where(x =>
            x.VariantId == productVariantId && x.LocationId == locationId)
            .Select(x => (int?)x.QuantityOnHand).SingleOrDefaultAsync(ct) ?? 0;

    private static InventoryPolicyDto Map(InventoryPolicy policy, int quantity) =>
        new(policy.ProductVariantId, policy.LocationId, policy.ReorderPoint,
            policy.TargetStockLevel, quantity, quantity < policy.ReorderPoint,
            quantity == 0, policy.UpdatedAt);
}
