using Microsoft.EntityFrameworkCore;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.Exceptions;
using SimpliaDz.Inventory.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class StockBalanceTests
{
    [Fact]
    public void Balance_starts_at_zero_and_rejects_negative_physical_stock()
    {
        var balance = new StockBalance(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(0, balance.QuantityOnHand);

        balance.ApplyDelta(7);
        balance.ApplyDelta(-3);
        Assert.Equal(4, balance.QuantityOnHand);
        Assert.Throws<InsufficientStockException>(() => balance.ApplyDelta(-5));
        Assert.Equal(4, balance.QuantityOnHand);
    }

    [Fact]
    public void Balance_has_unique_pair_and_database_concurrency_token()
    {
        using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var entity = db.Model.FindEntityType(typeof(StockBalance))!;
        Assert.Equal("StockItems", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockBalance.VariantId), nameof(StockBalance.LocationId)]));
        Assert.True(entity.FindProperty(nameof(StockBalance.RowVersion))!.IsConcurrencyToken);
    }
}
