using Microsoft.EntityFrameworkCore;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.Exceptions;
using SimpliaDz.Inventory.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class ManualStockAdjustmentTests
{
    [Fact]
    public void Positive_and_negative_adjustments_change_physical_stock_and_keep_the_actor()
    {
        var variantId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var actor = Guid.NewGuid().ToString();
        var balance = new StockBalance(variantId, locationId);
        var increase = new StockMovement(variantId, locationId, StockMovementType.Adjustment,
            8, actor, reason: "Found stock during count");
        balance.ApplyDelta(increase.QuantityDelta);
        var decrease = new StockMovement(variantId, locationId, StockMovementType.Adjustment,
            -3, actor, reason: "Damaged items");
        balance.ApplyDelta(decrease.QuantityDelta);

        Assert.Equal(5, balance.QuantityOnHand);
        Assert.Equal("Damaged items", decrease.Reason);
        Assert.Equal(actor, decrease.CreatedBy);
        Assert.Throws<InsufficientStockException>(() => balance.ApplyDelta(-6));
        Assert.Equal(5, balance.QuantityOnHand);
    }

    [Fact]
    public void Balance_change_cannot_be_saved_with_an_opposite_signed_movement()
    {
        using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var variantId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var balance = new StockBalance(variantId, locationId);
        balance.ApplyDelta(5);
        db.StockBalances.Attach(balance);
        balance.ApplyDelta(-2);
        db.StockMovements.Add(new StockMovement(variantId, locationId,
            StockMovementType.Adjustment, 2, "user", reason: "Incorrect sign"));

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
}
