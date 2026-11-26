using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Purchasing.Core.Entities;
using SimpliaDz.Purchasing.Infrastructure.Data;
using SimpliaDz.Purchasing.UseCases.Services;

namespace SimpliaDz.Api.Modules;

public sealed class GoodsReceiptService(PurchasingDbContext purchasing, ITenantContext tenant)
    : IGoodsReceiptService
{
    public async Task<GoodsReceiptDto> ReceiveAsync(Guid receiptId, Guid purchaseOrderId,
        IReadOnlyList<ReceiveGoodsLine> lines, string receivedBy, CancellationToken ct,
        string? deliveryReference = null)
    {
        if (receiptId == Guid.Empty || purchaseOrderId == Guid.Empty || lines.Count == 0 ||
            lines.Any(x => x.PurchaseOrderLineId == Guid.Empty || x.QuantityReceived <= 0) ||
            lines.Select(x => x.PurchaseOrderLineId).Distinct().Count() != lines.Count)
            throw new ArgumentException("Receipt ID, purchase order, and unique positive-quantity lines are required.");
        if (!tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Inventory must be enabled to receive goods.");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var transaction = await purchasing.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await using var inventory = new InventoryDbContext(
                new DbContextOptionsBuilder<InventoryDbContext>()
                    .UseSqlServer(purchasing.Database.GetDbConnection()).Options);
            await inventory.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
            try
            {
                if (await purchasing.GoodsReceipts.AnyAsync(x => x.Id == receiptId, ct))
                    throw new ConflictException("Goods receipt has already been processed.");

                var order = await purchasing.PurchaseOrders.Include(x => x.Lines)
                    .FirstOrDefaultAsync(x => x.Id == purchaseOrderId, ct)
                    ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);
                if (order.Status is not (PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived))
                    throw new ConflictException("Only an ordered or partially received PO can receive goods.");
                if (!await inventory.Locations.AnyAsync(x => x.Id == order.DestinationLocationId && x.IsActive, ct))
                    throw new ConflictException("Purchase order destination location must be active.");

                var orderLines = order.Lines.ToDictionary(x => x.Id);
                var items = new List<GoodsReceiptLineItem>(lines.Count);
                foreach (var input in lines)
                {
                    if (!orderLines.TryGetValue(input.PurchaseOrderLineId, out var orderLine))
                        throw new ConflictException("Receipt line must belong to the purchase order.");
                    if (input.QuantityReceived > orderLine.OrderedQuantity - orderLine.ReceivedQuantity)
                        throw new ConflictException("Cumulative received quantity exceeds ordered quantity.");
                    items.Add(new GoodsReceiptLineItem(orderLine.Id, orderLine.ProductVariantId,
                        input.QuantityReceived, orderLine.UnitCost));
                }

                var variantIds = items.Select(x => x.ProductVariantId).ToArray();
                var activeCount = await inventory.ProductVariants.CountAsync(x =>
                    variantIds.Contains(x.Id) && x.IsActive && x.Product.IsActive, ct);
                if (activeCount != items.Count)
                    throw new ConflictException("All received variants and products must be active.");

                var balances = await inventory.StockBalances
                    .Where(x => x.LocationId == order.DestinationLocationId && variantIds.Contains(x.VariantId))
                    .ToDictionaryAsync(x => x.VariantId, ct);
                var receipt = new GoodsReceipt(receiptId, order.Id, order.DestinationLocationId,
                    receivedBy, items, deliveryReference);

                for (var index = 0; index < items.Count; index++)
                {
                    var item = items[index];
                    order.RecordReceipt(item.PurchaseOrderLineId, item.QuantityReceived);
                    if (!balances.TryGetValue(item.ProductVariantId, out var balance))
                    {
                        balance = new StockBalance(item.ProductVariantId, order.DestinationLocationId);
                        inventory.StockBalances.Add(balance);
                        balances.Add(item.ProductVariantId, balance);
                    }
                    balance.ApplyDelta(item.QuantityReceived);
                    inventory.StockMovements.Add(new StockMovement(item.ProductVariantId,
                        order.DestinationLocationId, StockMovementType.PurchaseReceipt,
                        item.QuantityReceived, receivedBy, reference: order.PurchaseOrderNumber,
                        referenceType: "GoodsReceipt", referenceId: receipt.Id,
                        sourceLineNumber: index + 1));
                }

                purchasing.GoodsReceipts.Add(receipt);
                await purchasing.SaveChangesAsync(ct);
                await inventory.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Map(receipt);
            }
            catch (Exception exception) when (attempt < 4 && IsRetryable(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                purchasing.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                purchasing.ChangeTracker.Clear();
                throw new ConflictException("Goods receipt has already been processed or stock changed concurrently.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                purchasing.ChangeTracker.Clear();
                throw;
            }
        }

        throw new ConflictException("Goods receipt could not be processed due to concurrent updates.");
    }

    public async Task<GoodsReceiptDto> GetByIdAsync(Guid receiptId, CancellationToken ct) =>
        Map(await purchasing.GoodsReceipts.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == receiptId, ct)
            ?? throw new NotFoundException("GoodsReceipt", receiptId));

    public async Task<IReadOnlyList<GoodsReceiptDto>> ListByPurchaseOrderAsync(Guid purchaseOrderId,
        CancellationToken ct)
    {
        if (!await purchasing.PurchaseOrders.AnyAsync(x => x.Id == purchaseOrderId, ct))
            throw new NotFoundException("PurchaseOrder", purchaseOrderId);
        var receipts = await purchasing.GoodsReceipts.AsNoTracking().Include(x => x.Lines)
            .Where(x => x.PurchaseOrderId == purchaseOrderId)
            .OrderBy(x => x.ReceivedAt).ThenBy(x => x.Id).ToListAsync(ct);
        return receipts.Select(Map).ToList();
    }

    private static bool IsRetryable(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is DbUpdateException { InnerException: SqlException { Number: 1205 } } ||
        exception is SqlException { Number: 1205 };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static GoodsReceiptDto Map(GoodsReceipt x) => new(x.Id, x.PurchaseOrderId,
        x.LocationId, x.ReceivedAt, x.ReceivedBy,
        x.Lines.Select(line => new GoodsReceiptLineDto(line.Id, line.PurchaseOrderLineId,
            line.ProductVariantId, line.QuantityReceived, line.UnitCost)).ToList(), x.DeliveryReference);
}
