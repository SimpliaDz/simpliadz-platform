using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Sales.Core.Entities;
using Simplia.Sales.Infrastructure.Data;
using Simplia.Sales.UseCases.Services;

namespace Simplia.Api.Modules;

public sealed class SaleReturnService(SalesDbContext sales, ITenantContext tenant)
    : ISaleReturnService
{
    public async Task<SaleReturnDto> ProcessAsync(Guid id, Guid saleId, Guid locationId,
        string reason, Guid createdBy, IReadOnlyList<ReturnSaleLine> lines,
        CancellationToken ct)
    {
        if (id == Guid.Empty || saleId == Guid.Empty || locationId == Guid.Empty ||
            createdBy == Guid.Empty)
            throw new ArgumentException("Return, sale, location, and actor IDs are required.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("Reason is required and cannot exceed 500 characters.", nameof(reason));
        if (lines is null || lines.Count == 0 ||
            lines.Any(x => x.OriginalSaleLineId == Guid.Empty || x.Quantity <= 0) ||
            lines.Select(x => x.OriginalSaleLineId).Distinct().Count() != lines.Count)
            throw new ArgumentException("Return requires unique sale lines with positive quantities.", nameof(lines));
        if (!tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Inventory must be enabled for returns.");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var tx = await sales.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await using var stock = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
                .UseSqlServer(sales.Database.GetDbConnection()).Options);
            await stock.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
            try
            {
                if (await sales.SaleReturns.AnyAsync(x => x.Id == id, ct))
                    throw new ConflictException("Return has already been processed.");
                var sale = await sales.Sales.Include(x => x.Lines)
                    .FirstOrDefaultAsync(x => x.Id == saleId, ct)
                    ?? throw new NotFoundException("Sale", saleId);
                if (sale.Status == SaleStatus.Completed && sale.CustomerId.HasValue && sale.BalanceDue > 0)
                    throw new ConflictException("Settle the customer balance before processing a return.");
                if (sale.Status is not (SaleStatus.Completed or SaleStatus.PartiallyReturned))
                    throw new ConflictException("Only completed sales with returnable lines can be returned.");
                if (!await stock.Locations.AnyAsync(x => x.Id == locationId && x.IsActive, ct))
                    throw new NotFoundException("Active location", locationId);

                var originalLines = sale.Lines.ToDictionary(x => x.Id);
                foreach (var line in lines)
                {
                    if (!originalLines.TryGetValue(line.OriginalSaleLineId, out var original))
                        throw new NotFoundException("Sale line", line.OriginalSaleLineId);
                    if (line.Quantity > original.Quantity - original.ReturnedQuantity)
                        throw new ConflictException("Return quantity exceeds the remaining sold quantity.");
                }

                var discountedLinesTotal = sale.Lines.Sum(x => x.LineTotal);
                var items = lines.Select(line =>
                {
                    var original = originalLines[line.OriginalSaleLineId];
                    var netLine = discountedLinesTotal == 0m ? 0m :
                        original.LineTotal - (sale.DiscountAmount * original.LineTotal / discountedLinesTotal);
                    var unitRefund = decimal.Round(netLine / original.Quantity, 4,
                        MidpointRounding.ToZero);
                    return new SaleReturnLineItem(original.Id, original.ProductVariantId,
                        line.Quantity, unitRefund, line.Restock);
                }).ToList();
                var saleReturn = new SaleReturn(id, saleId, locationId, reason, createdBy, items);

                var restocks = saleReturn.Lines.Where(x => x.Restock).ToList();
                var variantIds = restocks.Select(x => x.ProductVariantId).Distinct().ToArray();
                var existing = await stock.StockBalances.Where(x =>
                    x.LocationId == locationId && variantIds.Contains(x.VariantId))
                    .ToDictionaryAsync(x => x.VariantId, ct);
                foreach (var item in restocks)
                {
                    if (!existing.TryGetValue(item.ProductVariantId, out var balance))
                    {
                        balance = new StockBalance(item.ProductVariantId, locationId);
                        stock.StockBalances.Add(balance);
                        existing.Add(item.ProductVariantId, balance);
                    }
                    balance.ApplyDelta(item.Quantity);
                    stock.StockMovements.Add(new StockMovement(item.ProductVariantId,
                        locationId, StockMovementType.SaleReturn, item.Quantity,
                        createdBy.ToString(), reference: saleReturn.ReturnNumber,
                        reason: saleReturn.Reason, referenceType: "SaleReturn",
                        referenceId: id, sourceLineNumber: saleReturn.Lines.ToList().IndexOf(item) + 1));
                }
                foreach (var line in lines)
                    sale.RecordReturn(line.OriginalSaleLineId, line.Quantity);
                sales.SaleReturns.Add(saleReturn);
                await sales.SaveChangesAsync(ct);
                await stock.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Map(saleReturn);
            }
            catch (Exception ex) when (attempt < 4 && IsRetryable(ex))
            {
                await tx.RollbackAsync(CancellationToken.None);
                sales.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                await tx.RollbackAsync(CancellationToken.None);
                sales.ChangeTracker.Clear();
                throw new ConflictException("Return already exists or stock changed concurrently.");
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                sales.ChangeTracker.Clear();
                throw;
            }
        }
        throw new ConflictException("Return changed concurrently; retry the request.");
    }

    public async Task<SaleReturnDto> GetByIdAsync(Guid id, CancellationToken ct) =>
        Map(await sales.SaleReturns.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Sale return", id));

    public async Task<IReadOnlyList<SaleReturnDto>> ListBySaleAsync(Guid saleId,
        CancellationToken ct)
    {
        if (!await sales.Sales.AnyAsync(x => x.Id == saleId, ct))
            throw new NotFoundException("Sale", saleId);
        var returns = await sales.SaleReturns.AsNoTracking().Include(x => x.Lines)
            .Where(x => x.OriginalSaleId == saleId)
            .OrderBy(x => x.ReturnDate).ThenBy(x => x.Id).ToListAsync(ct);
        return returns.Select(Map).ToList();
    }

    private static SaleReturnDto Map(SaleReturn x) => new(x.Id, x.ReturnNumber,
        x.OriginalSaleId, x.LocationId, x.ReturnDate, x.Reason, x.CreatedBy,
        x.RefundTotal, x.Lines.OrderBy(line => line.Id).Select(line =>
            new SaleReturnLineDto(line.Id, line.OriginalSaleLineId,
                line.ProductVariantId, line.Quantity, line.UnitRefundAmount,
                line.RefundAmount, line.Restock)).ToList());

    private static bool IsRetryable(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is DbUpdateException { InnerException: SqlException { Number: 1205 } } ||
        exception is SqlException { Number: 1205 };
}
