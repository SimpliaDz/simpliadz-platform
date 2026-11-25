using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Exceptions;
using SimpliaDz.Inventory.Core.ValueObjects;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Purchasing.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.UseCases.Services;

namespace SimpliaDz.Api.Modules;

public sealed class SaleService(SalesDbContext sales, InventoryDbContext inventory,
    PurchasingDbContext purchasing, ITenantContext tenant) : ISaleService
{
    public async Task<SaleDto> CreateDraftAsync(Guid locationId, Guid? customerId,
        Guid cashierId, Guid priceListId, CancellationToken ct)
    {
        var sale = await PrepareDraftAsync(locationId, customerId, cashierId, priceListId, ct);
        sales.Sales.Add(sale);
        await SaveAsync(ct);
        return Map(sale);
    }

    public async Task<SaleDto> StartWithItemAsync(Guid locationId, Guid? customerId, Guid cashierId,
        Guid priceListId, string code, CancellationToken ct, bool checkStock = true)
    {
        var sale = await PrepareDraftAsync(locationId, customerId, cashierId, priceListId, ct);
        try
        {
            await AddLineCoreAsync(sale, null, code, 1, 0, ct, checkStock);
        }
        catch (NotFoundException)
        {
            await AddLineCoreAsync(sale, code, null, 1, 0, ct, checkStock);
        }
        sales.Sales.Add(sale);
        await SaveAsync(ct);
        return await MapWithProductNamesAsync(sale, ct);
    }

    private async Task<Sale> PrepareDraftAsync(Guid locationId, Guid? customerId,
        Guid cashierId, Guid priceListId, CancellationToken ct)
    {
        if (!tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Inventory must be enabled for sales.");
        if (!await inventory.Locations.AnyAsync(x => x.Id == locationId && x.IsActive, ct))
            throw new NotFoundException("Active location", locationId);
        if (!await inventory.PriceLists.AnyAsync(x => x.Id == priceListId && x.IsActive, ct))
            throw new NotFoundException("Active price list", priceListId);
        if (customerId.HasValue && !await sales.Customers.AnyAsync(x =>
                x.Id == customerId && x.IsActive, ct))
            throw new NotFoundException("Active customer", customerId.Value);
        return new Sale(locationId, customerId, cashierId, priceListId);
    }

    public async Task<SaleDto> AddLineAsync(Guid saleId, string? sku, string? barcode,
        int quantity, decimal discountAmount, CancellationToken ct, bool checkStock = false)
    {
        var sale = await LoadAsync(saleId, ct);
        var existingIds = sale.Lines.Select(item => item.Id).ToHashSet();
        var line = await AddLineCoreAsync(sale, sku, barcode, quantity, discountAmount, ct, checkStock);
        if (!existingIds.Contains(line.Id))
            sales.SaleLines.Add(line);
        await SaveAsync(ct);
        return await MapWithProductNamesAsync(sale, ct);
    }

    private async Task<SaleLine> AddLineCoreAsync(Sale sale, string? sku, string? barcode,
        int quantity, decimal discountAmount, CancellationToken ct, bool checkStock)
    {
        if (string.IsNullOrWhiteSpace(sku) == string.IsNullOrWhiteSpace(barcode))
            throw new ArgumentException("Provide exactly one SKU or barcode.");
        if (!tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Inventory must be enabled for sales.");
        EnsureDraft(sale);
        var variant = !string.IsNullOrWhiteSpace(sku)
            ? await inventory.ProductVariants.AsNoTracking().FirstOrDefaultAsync(
                x => x.Sku == new Sku(sku.Trim()), ct)
            : await inventory.ProductVariants.AsNoTracking().FirstOrDefaultAsync(
                x => x.Barcode == barcode!.Trim(), ct);
        if (variant is null) throw new NotFoundException("Product variant", sku ?? barcode!);
        if (!variant.IsActive || !await inventory.Products.AnyAsync(x =>
                x.Id == variant.ProductId && x.IsActive, ct))
            throw new ConflictException("Product variant must be active.");
        var existingLine = sale.Lines.FirstOrDefault(x => x.ProductVariantId == variant.Id);
        if (checkStock)
            await EnsureStockAsync(sale.LocationId, variant.Id, (long)(existingLine?.Quantity ?? 0) + quantity, ct);
        var list = await inventory.PriceLists.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == sale.PriceListId && x.IsActive, ct);
        if (list is null)
            throw new ConflictException("Price list must be active.");
        var price = await inventory.PriceListItems.AsNoTracking()
            .Where(x => x.PriceListId == sale.PriceListId && x.ProductVariantId == variant.Id)
            .Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct);
        if (!price.HasValue)
            throw new ConflictException("Variant has no price in the selected price list.");
        var cost = tenant.IsModuleEnabled("Purchasing")
            ? await purchasing.GoodsReceiptLines.AsNoTracking()
                .Where(x => x.ProductVariantId == variant.Id)
                .OrderByDescending(x => x.GoodsReceipt.ReceivedAt).ThenByDescending(x => x.Id)
                .Select(x => (decimal?)x.UnitCost).FirstOrDefaultAsync(ct) ?? 0m
            : 0m;
        SalePromotion? promotion = null;
        if (list.Type == PriceListType.Retail && existingLine is null && discountAmount == 0)
        {
            var product = await inventory.Products.AsNoTracking().Where(x => x.Id == variant.ProductId)
                .Select(x => new { x.BrandId, x.ProductCategoryId }).SingleAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var candidates = await sales.Promotions.AsNoTracking().Where(x => x.IsActive &&
                x.StartsAt <= now && (x.EndsAt == null || x.EndsAt > now) &&
                ((x.Target == PromotionTarget.Category && x.TargetId == product.ProductCategoryId) ||
                 (x.Target == PromotionTarget.Brand && x.TargetId == product.BrandId) ||
                 (x.Target == PromotionTarget.Product && x.TargetId == variant.ProductId) ||
                 (x.Target == PromotionTarget.Variant && x.TargetId == variant.Id))).ToListAsync(ct);
            promotion = candidates.Where(x => x.UnitDiscount(price.Value) > 0)
                .OrderByDescending(x => x.UnitDiscount(price.Value)).ThenByDescending(x => x.Target)
                .ThenBy(x => x.Id).FirstOrDefault();
        }
        return sale.AddLine(variant.Id, quantity, price.Value, cost, discountAmount, promotion);
    }

    public async Task<SaleDto> SetCustomerAsync(Guid saleId, Guid? customerId,
        CancellationToken ct)
    {
        var sale = await LoadAsync(saleId, ct);
        EnsureDraft(sale);
        if (customerId.HasValue && !await sales.Customers.AnyAsync(x =>
                x.Id == customerId.Value && x.IsActive, ct))
            throw new NotFoundException("Active customer", customerId.Value);
        sale.SetCustomer(customerId);
        await SaveAsync(ct);
        return await MapWithProductNamesAsync(sale, ct);
    }

    public async Task<SaleDto> SetLineQuantityAsync(Guid saleId, Guid lineId, int quantity,
        CancellationToken ct, bool checkStock = false)
    {
        var sale = await LoadAsync(saleId, ct);
        EnsureDraft(sale);
        var line = sale.Lines.FirstOrDefault(x => x.Id == lineId)
            ?? throw new NotFoundException("Sale line", lineId);
        if (checkStock)
            await EnsureStockAsync(sale.LocationId, line.ProductVariantId, quantity, ct);
        sale.SetLineQuantity(lineId, quantity);
        await SaveAsync(ct);
        return await MapWithProductNamesAsync(sale, ct);
    }

    public async Task<SaleDto> RemoveLineAsync(Guid saleId, Guid lineId, CancellationToken ct)
    {
        var sale = await LoadAsync(saleId, ct);
        EnsureDraft(sale);
        var line = sale.Lines.FirstOrDefault(item => item.Id == lineId)
            ?? throw new NotFoundException("Sale line", lineId);
        // Mark the dependent deleted before severing its required relationship.
        sales.Entry(line).State = EntityState.Deleted;
        sale.RemoveLine(line);
        if (sale.Lines.Count == 0) sale.Cancel();
        await SaveAsync(ct);
        return await MapWithProductNamesAsync(sale, ct);
    }

    public async Task<SaleDto> SetDiscountAsync(Guid saleId, decimal discountAmount,
        CancellationToken ct)
        => await ApplySaleDiscountAsync(saleId, SaleDiscountType.FixedAmount,
            discountAmount, ct);

    public async Task<SaleDto> ApplySaleDiscountAsync(Guid saleId, SaleDiscountType type,
        decimal value, CancellationToken ct)
    {
        var sale = await LoadAsync(saleId, ct);
        EnsureDraft(sale);
        sale.ApplySaleDiscount(type, value);
        await SaveAsync(ct);
        return await MapWithProductNamesAsync(sale, ct);
    }

    public async Task<SaleDto> ApplyLineDiscountAsync(Guid saleId, Guid lineId,
        SaleDiscountType type, decimal value, CancellationToken ct)
    {
        var sale = await LoadAsync(saleId, ct);
        EnsureDraft(sale);
        if (!sale.Lines.Any(x => x.Id == lineId))
            throw new NotFoundException("Sale line", lineId);
        sale.ApplyLineDiscount(lineId, type, value);
        await SaveAsync(ct);
        return await MapWithProductNamesAsync(sale, ct);
    }

    public async Task<SaleDto> CompleteAsync(Guid saleId, Guid completedBy, CancellationToken ct,
        bool adjustShortfall = false)
    {
        if (!tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Inventory must be enabled for sales.");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var tx = await sales.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await using var stock = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
                .UseSqlServer(sales.Database.GetDbConnection()).Options);
            await stock.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
            try
            {
                var sale = await LoadAsync(saleId, ct);
                EnsureDraft(sale);
                if (sale.Lines.Count == 0)
                    throw new ConflictException("Add at least one line before completing a sale.");
                if (!await stock.Locations.AnyAsync(x => x.Id == sale.LocationId && x.IsActive, ct))
                    throw new ConflictException("Sale location must still be active.");
                var lines = sale.Lines.OrderBy(x => x.ProductVariantId).ToList();
                var ids = lines.Select(x => x.ProductVariantId).ToArray();
                var active = await stock.ProductVariants.CountAsync(x =>
                    ids.Contains(x.Id) && x.IsActive && x.Product.IsActive, ct);
                if (active != lines.Count)
                    throw new ConflictException("All sale variants must still be active.");
                var balances = await stock.StockBalances
                    .Where(x => x.LocationId == sale.LocationId && ids.Contains(x.VariantId))
                    .ToDictionaryAsync(x => x.VariantId, ct);
                if (!adjustShortfall && lines.Any(x => !balances.TryGetValue(x.ProductVariantId, out var balance) ||
                    balance.QuantityOnHand < x.Quantity))
                    throw new InsufficientStockException();

                sale.Complete(completedBy);
                for (var index = 0; index < lines.Count; index++)
                {
                    var line = lines[index];
                    if (!balances.TryGetValue(line.ProductVariantId, out var balance))
                    {
                        balance = new StockBalance(line.ProductVariantId, sale.LocationId);
                        stock.StockBalances.Add(balance);
                        stock.StockMovements.Add(new StockMovement(line.ProductVariantId, sale.LocationId,
                            StockMovementType.OpeningBalance, 0, completedBy.ToString(),
                            reason: "Initial balance for POS reconciliation."));
                    }
                    var shortfall = line.Quantity - balance.QuantityOnHand;
                    if (shortfall > 0)
                    {
                        stock.StockMovements.Add(new StockMovement(line.ProductVariantId,
                            sale.LocationId, StockMovementType.Adjustment, shortfall, completedBy.ToString(),
                            reference: sale.SaleNumber, reason: "POS stock discrepancy: physical units confirmed; recount recommended.",
                            referenceType: "PosStockDiscrepancy", referenceId: sale.Id, sourceLineNumber: index + 1));
                    }
                    var decrease = Math.Min(line.Quantity, balance.QuantityOnHand);
                    if (decrease > 0) balance.ApplyDelta(-decrease);
                    stock.StockMovements.Add(new StockMovement(line.ProductVariantId,
                        sale.LocationId, StockMovementType.Sale, -line.Quantity,
                        completedBy.ToString(), reference: sale.SaleNumber,
                        referenceType: "Sale", referenceId: sale.Id,
                        sourceLineNumber: index + 1));
                }
                await sales.SaveChangesAsync(ct);
                await stock.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Map(sale);
            }
            catch (Exception exception) when (attempt < 4 && IsRetryable(exception))
            {
                await tx.RollbackAsync(CancellationToken.None);
                sales.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (IsUniqueConflict(exception))
            {
                await tx.RollbackAsync(CancellationToken.None);
                sales.ChangeTracker.Clear();
                throw new ConflictException("Sale or stock changed concurrently; retry the request.");
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                sales.ChangeTracker.Clear();
                throw;
            }
        }
        throw new ConflictException("Sale changed concurrently; retry the request.");
    }

    public async Task CancelAsync(Guid saleId, CancellationToken ct)
    {
        var sale = await LoadAsync(saleId, ct);
        EnsureDraft(sale);
        sale.Cancel();
        await SaveAsync(ct);
    }

    public async Task<SaleDto> GetByIdAsync(Guid saleId, CancellationToken ct) =>
        await MapWithProductNamesAsync(await LoadAsync(saleId, ct), ct);

    private async Task EnsureStockAsync(Guid locationId, Guid variantId, long requested, CancellationToken ct)
    {
        var available = await inventory.StockBalances.AsNoTracking()
            .Where(x => x.LocationId == locationId && x.VariantId == variantId)
            .Select(x => (int?)x.QuantityOnHand).SingleOrDefaultAsync(ct) ?? 0;
        if (requested > available)
            throw new InsufficientStockException(available);
    }

    private async Task<SaleDto> MapWithProductNamesAsync(Sale sale, CancellationToken ct)
    {
        var dto = Map(sale);
        if (dto.Lines.Count == 0 || !tenant.IsModuleEnabled("Inventory")) return dto;

        var ids = dto.Lines.Select(line => line.ProductVariantId).Distinct().ToArray();
        var stock = await inventory.StockBalances.AsNoTracking()
            .Where(x => x.LocationId == sale.LocationId && ids.Contains(x.VariantId))
            .ToDictionaryAsync(x => x.VariantId, x => x.QuantityOnHand, ct);
        var variants = await inventory.ProductVariants.AsNoTracking()
            .Where(variant => ids.Contains(variant.Id))
            .Select(variant => new
            {
                variant.Id,
                ProductName = variant.Product.Name,
                variant.Sku,
                variant.Color,
                variant.Size,
                Attributes = variant.AttributeValues.Select(value => value.AttributeValue.Value)
                    .OrderBy(value => value).ToList()
            })
            .ToDictionaryAsync(variant => variant.Id, ct);

        return dto with
        {
            Lines = dto.Lines.Select(line =>
            {
                if (!variants.TryGetValue(line.ProductVariantId, out var variant)) return line;
                IEnumerable<string?> descriptors = variant.Attributes.Count > 0
                    ? variant.Attributes
                    : new[] { variant.Color, variant.Size };
                var name = string.Join(" / ", descriptors.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
                return line with { ProductName = variant.ProductName, VariantName = name, Sku = variant.Sku.Value,
                    AvailableQuantity = stock.GetValueOrDefault(line.ProductVariantId) };
            }).ToArray()
        };
    }

    public async Task<IReadOnlyList<SaleDto>> ListAsync(Guid? locationId, Guid? customerId,
        string? status, string? search, int page, int pageSize, CancellationToken ct)
        => await ReadPageAsync(FilterSales(locationId, customerId, status, search), page, pageSize, ct);

    public async Task<SaleHistoryDto> HistoryAsync(Guid? locationId, Guid? customerId, string? status,
        string? search, DateTimeOffset? from, DateTimeOffset? until, int page, int pageSize, CancellationToken ct)
    {
        var query = FilterSales(locationId, customerId, status, search);
        if (from.HasValue) query = query.Where(x => x.SaleDate >= from.Value);
        if (until.HasValue) query = query.Where(x => x.SaleDate < until.Value);
        var summary = await query.GroupBy(_ => 1)
            .Select(group => new { Count = group.LongCount(), Total = group.Sum(x => x.Total) })
            .SingleOrDefaultAsync(ct);
        var items = await ReadPageAsync(query, page, pageSize, ct);
        return new SaleHistoryDto(items, summary?.Count ?? 0, summary?.Total ?? 0);
    }

    private IQueryable<Sale> FilterSales(Guid? locationId, Guid? customerId, string? status, string? search)
    {
        var query = sales.Sales.AsNoTracking().Where(sale => sale.Lines.Any());
        if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId.Value);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SaleStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ArgumentException("Invalid sale status.", nameof(status));
            query = query.Where(x => x.Status == parsed);
        }
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            if (term.Length > 150) throw new ArgumentException("Search must fit 150 characters.", nameof(search));
            var matchId = Guid.TryParse(term, out var saleId);
            query = query.Where(x => (matchId && x.Id == saleId) ||
                (x.SaleNumber != null && x.SaleNumber.Contains(term)) ||
                (x.Customer != null && (x.Customer.Name.Contains(term) ||
                    (x.Customer.Phone != null && x.Customer.Phone.Contains(term)) ||
                    (x.Customer.Email != null && x.Customer.Email.Contains(term)))));
        }
        return query;
    }

    private static async Task<IReadOnlyList<SaleDto>> ReadPageAsync(
        IQueryable<Sale> query, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        var result = await query.Include(x => x.Lines).OrderByDescending(x => x.SaleDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return result.Select(Map).ToList();
    }

    private async Task<Sale> LoadAsync(Guid id, CancellationToken ct) =>
        await sales.Sales.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Sale", id);

    private static void EnsureDraft(Sale sale)
    {
        if (sale.Status != SaleStatus.Draft)
            throw new ConflictException("Only a draft sale can be changed or completed.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await sales.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Sale changed concurrently; reload and retry.");
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            throw new ConflictException("Sale line already exists or sale changed concurrently.");
        }
    }

    private static bool IsUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool IsRetryable(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } } ||
        exception is SqlException { Number: 1205 };

    private static SaleDto Map(Sale x) => new(x.Id, x.SaleNumber, x.LocationId, x.CustomerId,
        x.CashierId, x.PriceListId, x.Status.ToString(), x.SaleDate, x.Subtotal,
        x.DiscountAmount, x.Total, x.Lines.OrderBy(line => line.ProductVariantId)
            .Select(line => new SaleLineDto(line.Id, line.ProductVariantId, line.Quantity,
                line.UnitPrice, line.UnitCostSnapshot, line.DiscountAmount, line.LineTotal,
                line.DiscountType?.ToString(), line.DiscountValue, line.ReturnedQuantity)
                { PromotionId = line.PromotionId, PromotionName = line.PromotionName,
                    PromotionUnitDiscount = line.PromotionUnitDiscount }).ToList(),
        x.DiscountType?.ToString(), x.DiscountValue, x.PaidAmount, x.BalanceDue);
}
