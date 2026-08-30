using Microsoft.EntityFrameworkCore;
using SimpliaDz.Inventory.Core.Entities;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Seeders;

internal static class InventorySeeder
{
    public static async Task SeedAsync(InventoryDbContext db, CancellationToken ct)
    {
        await SeedLocationsAsync(db, ct);
        await SeedBrandsAsync(db, ct);
        await SeedCategoriesAsync(db, ct);
        await SeedProductsAsync(db, ct);

        // optional but very useful for testing
        await SeedInitialStockAsync(db, ct);
    }

    private static async Task SeedLocationsAsync(InventoryDbContext db, CancellationToken ct)
    {
        await UpsertLocation(db, "Main Store", "MAIN", LocationType.Store, ct);
        await UpsertLocation(db, "Warehouse", "WH", LocationType.Warehouse, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertLocation(
        InventoryDbContext db,
        string name,
        string code,
        LocationType type,
        CancellationToken ct)
    {
        var existing = await db.Locations.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (existing is not null) return;

        var loc = new InventoryLocation(name, code, type);
        db.Locations.Add(loc);
    }

    private static async Task SeedBrandsAsync(InventoryDbContext db, CancellationToken ct)
    {
        await UpsertBrand(db, "Nike", ct);
        await UpsertBrand(db, "Adidas", ct);
        await UpsertBrand(db, "Generic", ct);
        await UpsertBrand(db, "Puma", ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertBrand(InventoryDbContext db, string name, CancellationToken ct)
    {
        var exists = await db.Brands.AnyAsync(x => x.Name == name, ct);
        if (exists) return;

        db.Brands.Add(new Brand(name));
    }

    private static async Task SeedCategoriesAsync(InventoryDbContext db, CancellationToken ct)
    {
        await UpsertCategory(db, "T-Shirts", ct);
        await UpsertCategory(db, "Jeans", ct);
        await UpsertCategory(db, "Shoes", ct);
        await UpsertCategory(db, "Jackets", ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertCategory(InventoryDbContext db, string name, CancellationToken ct)
    {
        var exists = await db.Categories.AnyAsync(x => x.Name == name, ct);
        if (exists) return;

        db.Categories.Add(new ProductCategory(name));
    }

    private static async Task SeedProductsAsync(InventoryDbContext db, CancellationToken ct)
    {
        // If at least one product exists, we still might want to add missing ones.
        // We'll upsert by Product.Code (unique-ish for dev seeds).

        var brandGenericId = await db.Brands.Where(b => b.Name == "Generic").Select(b => b.Id).FirstAsync(ct);
        var brandNikeId = await db.Brands.Where(b => b.Name == "Nike").Select(b => b.Id).FirstAsync(ct);
        var brandAdidasId = await db.Brands.Where(b => b.Name == "Adidas").Select(b => b.Id).FirstAsync(ct);

        var catTshirtsId = await db.Categories.Where(c => c.Name == "T-Shirts").Select(c => c.Id).FirstAsync(ct);
        var catJeansId = await db.Categories.Where(c => c.Name == "Jeans").Select(c => c.Id).FirstAsync(ct);
        var catShoesId = await db.Categories.Where(c => c.Name == "Shoes").Select(c => c.Id).FirstAsync(ct);

        await UpsertProductWithVariants(
            db,
            name: "Basic T-Shirt",
            code: "TSHIRT",
            description: "Simple cotton t-shirt",
            brandId: brandGenericId,
            categoryId: catTshirtsId,
            variants: new[]
            {
                new VariantSeed("TSHIRT-BLK-M", 1500m, 900m, "Black", "M", "6221000000011"),
                new VariantSeed("TSHIRT-WHT-L", 1500m, 900m, "White", "L", "6221000000012"),
            },
            ct
        );

        await UpsertProductWithVariants(
            db,
            name: "Nike Hoodie",
            code: "HOODIE-NIKE",
            description: "Warm hoodie for winter",
            brandId: brandNikeId,
            categoryId: catTshirtsId,
            variants: new[]
            {
                new VariantSeed("NK-HOOD-BLK-M", 9500m, 7200m, "Black", "M", "6221000000101"),
                new VariantSeed("NK-HOOD-GRY-L", 9500m, 7200m, "Grey", "L", "6221000000102"),
            },
            ct
        );

        await UpsertProductWithVariants(
            db,
            name: "Adidas Jeans",
            code: "JEAN-ADIDAS",
            description: "Slim fit jeans",
            brandId: brandAdidasId,
            categoryId: catJeansId,
            variants: new[]
            {
                new VariantSeed("AD-JEAN-BLU-32", 8500m, 6500m, "Blue", "32", "6221000000201"),
                new VariantSeed("AD-JEAN-BLK-34", 8500m, 6500m, "Black", "34", "6221000000202"),
            },
            ct
        );

        await UpsertProductWithVariants(
            db,
            name: "Running Shoes",
            code: "SHOES-RUN",
            description: "Comfort running shoes",
            brandId: brandGenericId,
            categoryId: catShoesId,
            variants: new[]
            {
                new VariantSeed("RUN-SHOE-WHT-42", 12000m, 9500m, "White", "42", "6221000000301"),
                new VariantSeed("RUN-SHOE-BLK-43", 12000m, 9500m, "Black", "43", "6221000000302"),
            },
            ct
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertProductWithVariants(
        InventoryDbContext db,
        string name,
        string code,
        string? description,
        Guid brandId,
        Guid categoryId,
        IEnumerable<VariantSeed> variants,
        CancellationToken ct)
    {
        var existing = await db.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Code == code, ct);

        if (existing is null)
        {
            var p = new Product(name, code: code, description: description);
            p.AssignBrand(brandId);
            p.AssignCategory(categoryId);

            foreach (var v in variants)
            {
                p.AddVariant(
                    sku: v.Sku,
                    retailPrice: v.RetailPrice,
                    wholesalePrice: v.WholesalePrice,
                    barcode: v.Barcode,
                    color: v.Color,
                    size: v.Size
                );
            }

            db.Products.Add(p);
            return;
        }

        // If product exists, only add missing variants by SKU
        foreach (var v in variants)
        {
            var skuExists = existing.Variants.Any(x => x.Sku.Value == v.Sku);
            if (!skuExists)
            {
                existing.AddVariant(
                    sku: v.Sku,
                    retailPrice: v.RetailPrice,
                    wholesalePrice: v.WholesalePrice,
                    barcode: v.Barcode,
                    color: v.Color,
                    size: v.Size
                );
            }
        }
    }

    private static async Task SeedInitialStockAsync(InventoryDbContext db, CancellationToken ct)
    {
        // Put initial quantities in MAIN and WH for some SKUs
        var mainId = await db.Locations.Where(l => l.Code == "MAIN").Select(l => l.Id).FirstAsync(ct);
        var whId = await db.Locations.Where(l => l.Code == "WH").Select(l => l.Id).FirstAsync(ct);

        // Load variants (we seed by SKU)
        var variants = await db.ProductVariants
            .Select(v => new { v.Id, Sku = v.Sku.Value })
            .ToListAsync(ct);

        // Simple demo quantities
        var qtyMap = new Dictionary<string, (int main, int wh)>(StringComparer.OrdinalIgnoreCase)
        {
            ["TSHIRT-BLK-M"] = (main: 12, wh: 50),
            ["TSHIRT-WHT-L"] = (main: 8,  wh: 40),
            ["NK-HOOD-BLK-M"] = (main: 5,  wh: 20),
            ["AD-JEAN-BLU-32"] = (main: 6,  wh: 15),
            ["RUN-SHOE-WHT-42"] = (main: 4,  wh: 10),
        };

        foreach (var v in variants)
        {
            if (!qtyMap.TryGetValue(v.Sku, out var q)) continue;

            await EnsureStockAsync(db, v.Id, mainId, q.main, reference: "SEED", ct);
            await EnsureStockAsync(db, v.Id, whId, q.wh, reference: "SEED", ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureStockAsync(
        InventoryDbContext db,
        Guid variantId,
        Guid locationId,
        int qty,
        string reference,
        CancellationToken ct)
    {
        if (qty <= 0) return;

        // Snapshot row
        var stockItem = await db.StockItems
            .FirstOrDefaultAsync(s => s.VariantId == variantId && s.LocationId == locationId, ct);

        if (stockItem is null)
        {
            stockItem = new StockItem(variantId, locationId, initialQty: qty);
            db.StockItems.Add(stockItem);
        }
        else if (stockItem.QuantityOnHand == 0)
        {
            // keep idempotent-ish: only seed if empty
            stockItem.SetQuantity(qty);
        }
        else
        {
            return; // already has stock, don't keep adding
        }

        // Ledger row (optional but recommended)
        var hasSeedMove = await db.StockMovements.AnyAsync(m =>
            m.VariantId == variantId &&
            m.LocationId == locationId &&
            m.Reference == reference &&
            m.Type == StockMovementType.Receipt, ct);

        if (!hasSeedMove)
        {
            db.StockMovements.Add(new StockMovement(
                variantId: variantId,
                locationId: locationId,
                type: StockMovementType.Receipt,
                quantityDelta: qty,
                reference: reference,
                note: "Initial seed stock",
                sourceDocumentType: "Seeder",
                sourceDocumentId: null,
                sourceLineNumber: null
            ));
        }
    }

    private readonly record struct VariantSeed(
        string Sku,
        decimal RetailPrice,
        decimal? WholesalePrice,
        string? Color,
        string? Size,
        string? Barcode
    );
}
