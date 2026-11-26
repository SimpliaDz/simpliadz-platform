using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Sales.UseCases.Services;

namespace Simplia.Api.Modules;

public sealed class PosProductLookupService(InventoryDbContext inventory, ITenantContext tenant)
    : IPosProductLookupService
{
    public async Task<PosVariantDto?> FindByBarcodeAsync(string barcode, Guid priceListId,
        Guid? locationId, CancellationToken ct)
    {
        var value = NormalizeExact(barcode, nameof(barcode));
        await ValidateContextAsync(priceListId, locationId, ct);
        var row = await inventory.Database.SqlQuery<PosVariantRow>($"""
            SELECT p.Id AS ProductId, p.Name AS ProductName, v.Id AS VariantId,
                   v.Sku, v.Barcode, v.Color, v.Size, item.Price, list.CurrencyCode,
                   balance.QuantityOnHand
            FROM inventory.ProductVariants AS v
            JOIN inventory.Products AS p ON p.Id = v.ProductId
            JOIN inventory.PriceListItems AS item ON item.ProductVariantId = v.Id
            JOIN inventory.PriceLists AS list ON list.Id = item.PriceListId
            LEFT JOIN inventory.StockItems AS balance
                ON balance.VariantId = v.Id AND balance.LocationId = {locationId}
            WHERE v.Barcode = {value} AND item.PriceListId = {priceListId}
              AND v.IsActive = 1 AND p.IsActive = 1 AND list.IsActive = 1
            """).FirstOrDefaultAsync(ct);
        return row is null ? null : Map(row);
    }

    public async Task<PosVariantDto?> FindBySkuAsync(string sku, Guid priceListId,
        Guid? locationId, CancellationToken ct)
    {
        var value = NormalizeExact(sku, nameof(sku));
        await ValidateContextAsync(priceListId, locationId, ct);
        var row = await inventory.Database.SqlQuery<PosVariantRow>($"""
            SELECT p.Id AS ProductId, p.Name AS ProductName, v.Id AS VariantId,
                   v.Sku, v.Barcode, v.Color, v.Size, item.Price, list.CurrencyCode,
                   balance.QuantityOnHand
            FROM inventory.ProductVariants AS v
            JOIN inventory.Products AS p ON p.Id = v.ProductId
            JOIN inventory.PriceListItems AS item ON item.ProductVariantId = v.Id
            JOIN inventory.PriceLists AS list ON list.Id = item.PriceListId
            LEFT JOIN inventory.StockItems AS balance
                ON balance.VariantId = v.Id AND balance.LocationId = {locationId}
            WHERE v.Sku = {value} AND item.PriceListId = {priceListId}
              AND v.IsActive = 1 AND p.IsActive = 1 AND list.IsActive = 1
            """).FirstOrDefaultAsync(ct);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<PosVariantDto>> SearchByProductNameAsync(
        string namePrefix, Guid priceListId, Guid? locationId, int page, int pageSize,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(namePrefix) || namePrefix.Trim().Length > 200)
            throw new ArgumentException("Product name prefix must contain 1 to 200 characters.", nameof(namePrefix));
        if (page < 1 || pageSize is < 1 or > 50 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        await ValidateContextAsync(priceListId, locationId, ct);
        var prefix = EscapeLike(namePrefix.Trim()) + "%";
        var offset = (page - 1) * pageSize;
        var rows = await inventory.Database.SqlQuery<PosVariantRow>($"""
            SELECT p.Id AS ProductId, p.Name AS ProductName, v.Id AS VariantId,
                   v.Sku, v.Barcode, v.Color, v.Size, item.Price, list.CurrencyCode,
                   balance.QuantityOnHand
            FROM inventory.Products AS p
            JOIN inventory.ProductVariants AS v ON v.ProductId = p.Id
            JOIN inventory.PriceListItems AS item ON item.ProductVariantId = v.Id
            JOIN inventory.PriceLists AS list ON list.Id = item.PriceListId
            LEFT JOIN inventory.StockItems AS balance
                ON balance.VariantId = v.Id AND balance.LocationId = {locationId}
            WHERE p.Name LIKE {prefix} ESCAPE '\'
              AND item.PriceListId = {priceListId}
              AND p.IsActive = 1 AND v.IsActive = 1 AND list.IsActive = 1
              AND ({locationId} IS NULL OR balance.QuantityOnHand > 0)
            ORDER BY p.Name, v.Sku
            OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY
            """).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    private async Task ValidateContextAsync(Guid priceListId, Guid? locationId, CancellationToken ct)
    {
        if (!tenant.IsModuleEnabled("Inventory"))
            throw new ConflictException("Inventory must be enabled for POS lookup.");
        if (priceListId == Guid.Empty || !await inventory.PriceLists.AsNoTracking()
                .AnyAsync(x => x.Id == priceListId && x.IsActive, ct))
            throw new NotFoundException("Active price list", priceListId);
        if (locationId.HasValue && (locationId.Value == Guid.Empty ||
            !await inventory.Locations.AsNoTracking()
                .AnyAsync(x => x.Id == locationId.Value && x.IsActive, ct)))
            throw new NotFoundException("Active location", locationId.Value);
    }

    private static string NormalizeExact(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 64)
            throw new ArgumentException("Lookup code must contain 1 to 64 characters.", name);
        return value.Trim();
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal);

    private static PosVariantDto Map(PosVariantRow row) => new(row.ProductId, row.ProductName,
        row.VariantId, row.Sku, row.Barcode, row.Color, row.Size, row.Price,
        row.CurrencyCode, row.QuantityOnHand);

    public sealed class PosVariantRow
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public Guid VariantId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public decimal Price { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public int? QuantityOnHand { get; set; }
    }
}
