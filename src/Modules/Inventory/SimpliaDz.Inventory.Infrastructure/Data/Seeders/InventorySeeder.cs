using Microsoft.EntityFrameworkCore;
using SimpliaDz.Inventory.Core.Entities;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data.Seeders;

internal static class InventorySeeder
{
    public static async Task SeedAsync(InventoryDbContext db, CancellationToken ct)
    {
        // Always good to have deterministic behavior for dev seeds
        await SeedLocationsAsync(db, ct);
        await SeedBrandsAsync(db, ct);
        await SeedCategoriesAsync(db, ct);
        await SeedProductsAsync(db, ct);
    }

    private static async Task SeedLocationsAsync(InventoryDbContext db, CancellationToken ct)
    {
        if (await db.Locations.AnyAsync(ct)) return;

        db.Locations.AddRange(
            CreateLocation("Main Store", "MAIN"),
            CreateLocation("Warehouse", "WH")
        );

        await db.SaveChangesAsync(ct);
    }

    private static InventoryLocation CreateLocation(string name, string code)
    {
        var loc = new InventoryLocation(name);
        loc.SetCode(code); // if you implemented SetCode; otherwise remove this line
        return loc;
    }

    private static async Task SeedBrandsAsync(InventoryDbContext db, CancellationToken ct)
    {
        if (await db.Brands.AnyAsync(ct)) return;

        db.Brands.AddRange(
            new Brand("Nike"),
            new Brand("Adidas"),
            new Brand("Generic")
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCategoriesAsync(InventoryDbContext db, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;

        db.Categories.AddRange(
            new ProductCategory("T-Shirts"),
            new ProductCategory("Jeans"),
            new ProductCategory("Shoes")
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedProductsAsync(InventoryDbContext db, CancellationToken ct)
    {
        // Only seed sample products if none exist
        if (await db.Products.AnyAsync(ct)) return;

        var genericBrandId = await db.Brands
            .Where(b => b.Name == "Generic")
            .Select(b => b.Id)
            .FirstAsync(ct);

        var tshirtCategoryId = await db.Categories
            .Where(c => c.Name == "T-Shirts")
            .Select(c => c.Id)
            .FirstAsync(ct);

        var p1 = new Product("Basic T-Shirt", code: "TSHIRT", description: "Simple cotton t-shirt");
        p1.AssignBrand(genericBrandId);
        p1.AssignCategory(tshirtCategoryId);

        p1.AddVariant("TSHIRT-BLK-M", retailPrice: 1500m, wholesalePrice: 900m, color: "Black", size: "M");
        p1.AddVariant("TSHIRT-WHT-L", retailPrice: 1500m, wholesalePrice: 900m, color: "White", size: "L");

        db.Products.Add(p1);

        await db.SaveChangesAsync(ct);
    }
}
