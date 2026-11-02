using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Purchasing.Core.Entities;
using SimpliaDz.Purchasing.Infrastructure.Data;
using SimpliaDz.Purchasing.UseCases.Services;

namespace SimpliaDz.Purchasing.Infrastructure.Services;

internal sealed class PurchaseOrderService(PurchasingDbContext db,
    IInventoryReferenceValidator inventory) : IPurchaseOrderService
{
    public async Task<PurchaseOrderDto> CreateDraftAsync(Guid supplierId, Guid destinationLocationId,
        string? notes, string createdBy, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, ct)
            ?? throw new NotFoundException("Supplier", supplierId);
        if (!supplier.IsActive) throw new ConflictException("Supplier is inactive.");
        if (!await inventory.IsActiveLocationAsync(destinationLocationId, ct))
            throw new ConflictException("Destination location must be active.");
        var order = new PurchaseOrder(supplierId, destinationLocationId, createdBy, notes);
        db.PurchaseOrders.Add(order);
        await SaveAsync(ct);
        return Map(order);
    }

    public async Task<PurchaseOrderDto> AddLineAsync(Guid orderId, Guid productVariantId,
        int orderedQuantity, decimal unitCost, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, ct);
        EnsureDraft(order);
        if (!await inventory.IsActiveVariantAsync(productVariantId, ct))
            throw new ConflictException("Product variant must be active.");
        if (order.Lines.Any(x => x.ProductVariantId == productVariantId))
            throw new ConflictException("Variant already exists on this purchase order.");
        order.AddLine(productVariantId, orderedQuantity, unitCost);
        await SaveAsync(ct);
        return Map(order);
    }

    public async Task<PurchaseOrderDto> UpdateNotesAsync(Guid orderId, string? notes, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, ct);
        EnsureDraft(order);
        order.UpdateNotes(notes);
        await SaveAsync(ct);
        return Map(order);
    }

    public async Task<PurchaseOrderDto> PlaceOrderAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, ct);
        EnsureDraft(order);
        if (order.Lines.Count == 0) throw new ConflictException("A purchase order needs at least one line.");
        var supplier = await db.Suppliers.AsNoTracking().FirstAsync(x => x.Id == order.SupplierId, ct);
        if (!supplier.IsActive) throw new ConflictException("Supplier is inactive.");
        if (!await inventory.IsActiveLocationAsync(order.DestinationLocationId, ct))
            throw new ConflictException("Destination location must be active.");
        foreach (var line in order.Lines)
            if (!await inventory.IsActiveVariantAsync(line.ProductVariantId, ct))
                throw new ConflictException($"Product variant {line.ProductVariantId} must be active.");
        order.PlaceOrder();
        await SaveAsync(ct);
        return Map(order);
    }

    public async Task CancelAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, ct);
        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            throw new ConflictException("Only an unreceived purchase order can be cancelled.");
        order.Cancel();
        await SaveAsync(ct);
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(Guid orderId, CancellationToken ct) =>
        Map(await LoadAsync(orderId, ct));

    public async Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(Guid? supplierId, string? status,
        int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        var query = db.PurchaseOrders.AsNoTracking().Include(x => x.Lines).AsQueryable();
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PurchaseOrderStatus>(status, true, out var parsed) ||
                !Enum.IsDefined(parsed)) throw new ArgumentException("Invalid purchase order status.", nameof(status));
            query = query.Where(x => x.Status == parsed);
        }
        var orders = await query.OrderByDescending(x => x.OrderDate).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return orders.Select(Map).ToList();
    }

    private async Task<PurchaseOrder> LoadAsync(Guid id, CancellationToken ct) =>
        await db.PurchaseOrders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("PurchaseOrder", id);

    private static void EnsureDraft(PurchaseOrder order)
    {
        if (order.Status != PurchaseOrderStatus.Draft)
            throw new ConflictException("Only a draft purchase order can be edited.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Purchase order changed concurrently; reload and retry.");
        }
    }

    private static PurchaseOrderDto Map(PurchaseOrder x) => new(x.Id, x.PurchaseOrderNumber,
        x.SupplierId, x.DestinationLocationId, x.Status.ToString(), x.OrderDate, x.Notes,
        x.CreatedBy, x.Total, x.Lines.OrderBy(line => line.Id)
            .Select(line => new PurchaseOrderLineDto(line.Id, line.ProductVariantId,
                line.OrderedQuantity, line.ReceivedQuantity, line.UnitCost, line.LineTotal)).ToList());
}
