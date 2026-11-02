using Microsoft.EntityFrameworkCore;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class OpeningStockTests
{
    [Fact]
    public void Zero_opening_is_a_valid_auditable_event_but_other_zero_movements_are_not()
    {
        var variantId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var eventAt = DateTimeOffset.UtcNow;
        var movement = new StockMovement(variantId, locationId,
            StockMovementType.OpeningBalance, 0, "user-123", reason: "Counted at onboarding");

        Assert.Equal(0, movement.QuantityDelta);
        Assert.Equal(StockMovementType.OpeningBalance, movement.MovementType);
        Assert.Equal("user-123", movement.CreatedBy);
        Assert.True(movement.OccurredAt >= eventAt);
        Assert.Throws<ArgumentException>(() => new StockMovement(variantId, locationId,
            StockMovementType.Adjustment, 0, "user-123"));
        Assert.Throws<ArgumentException>(() => new StockMovement(variantId, locationId,
            StockMovementType.OpeningBalance, -1, "user-123"));
    }

    [Fact]
    public void Opening_is_unique_per_pair_in_the_database_model()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(StockMovement))!;
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.GetFilter() == "[Type] = 7" &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockMovement.VariantId), nameof(StockMovement.LocationId)]));
    }

    [Fact]
    public void Zero_balance_without_opening_event_cannot_be_saved()
    {
        using var db = CreateContext();
        db.StockBalances.Add(new StockBalance(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    private static InventoryDbContext CreateContext() => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
