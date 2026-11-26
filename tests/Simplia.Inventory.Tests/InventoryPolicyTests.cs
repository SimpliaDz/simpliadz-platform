using Microsoft.EntityFrameworkCore;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class InventoryPolicyTests
{
    [Fact]
    public void Policy_accepts_zero_threshold_and_optional_target()
    {
        var variant = Guid.NewGuid();
        var location = Guid.NewGuid();
        var policy = new InventoryPolicy(variant, location, 0, null);
        Assert.Equal(variant, policy.ProductVariantId);
        Assert.Equal(location, policy.LocationId);
        Assert.Equal(0, policy.ReorderPoint);
        Assert.Null(policy.TargetStockLevel);
        policy.Configure(5, 12);
        Assert.Equal(5, policy.ReorderPoint);
        Assert.Equal(12, policy.TargetStockLevel);
    }

    [Fact]
    public void Policy_rejects_negative_or_inconsistent_thresholds_without_mutating()
    {
        var policy = new InventoryPolicy(Guid.NewGuid(), Guid.NewGuid(), 5, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.Configure(-1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.Configure(8, 7));
        Assert.Equal(5, policy.ReorderPoint);
        Assert.Equal(10, policy.TargetStockLevel);
    }

    [Fact]
    public void Policy_has_unique_variant_location_key_and_does_not_change_stock_balance()
    {
        using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var model = db.Model.FindEntityType(typeof(InventoryPolicy))!;
        Assert.Equal("InventoryPolicies", model.GetTableName());
        Assert.Equal("inventory", model.GetSchema());
        Assert.Equal([nameof(InventoryPolicy.ProductVariantId), nameof(InventoryPolicy.LocationId)],
            model.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.True(model.FindProperty(nameof(InventoryPolicy.RowVersion))!.IsConcurrencyToken);
        Assert.Null(db.Model.FindEntityType(typeof(StockBalance))!
            .FindProperty("ReorderPoint"));
    }
}
