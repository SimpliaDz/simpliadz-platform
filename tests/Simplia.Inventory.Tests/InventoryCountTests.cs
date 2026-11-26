using Microsoft.EntityFrameworkCore;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class InventoryCountTests
{
    [Fact]
    public void Count_calculates_difference_and_locks_after_completion()
    {
        var count = new InventoryCount(Guid.NewGuid(), "user");
        var variantId = Guid.NewGuid();
        var line = count.AddLine(variantId, 17);
        Assert.Null(line.Difference);
        count.SetCountedQuantity(variantId, 15);
        Assert.Equal(-2, line.Difference);
        count.Complete("manager");
        Assert.Equal(InventoryCountStatus.Completed, count.Status);
        Assert.NotNull(count.CompletedAt);
        Assert.Throws<InvalidOperationException>(() => count.Complete("manager"));
        Assert.Throws<InvalidOperationException>(() => count.SetCountedQuantity(variantId, 17));
    }

    [Fact]
    public void Count_requires_nonnegative_unique_lines_and_all_physical_quantities()
    {
        var count = new InventoryCount(Guid.NewGuid(), "user");
        var variantId = Guid.NewGuid();
        count.AddLine(variantId, 0);
        Assert.Throws<InvalidOperationException>(() => count.AddLine(variantId, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => count.SetCountedQuantity(variantId, -1));
        Assert.Throws<InvalidOperationException>(() => count.Complete("manager"));
        count.SetCountedQuantity(variantId, 3);
        Assert.Equal(3, count.Lines.Single().Difference);
        count.Cancel();
        Assert.Equal(InventoryCountStatus.Cancelled, count.Status);
        Assert.Throws<InvalidOperationException>(() => count.Complete("manager"));
    }

    [Fact]
    public void Count_model_has_concurrency_and_history_constraints()
    {
        using var db = NewDb();
        var count = db.Model.FindEntityType(typeof(InventoryCount))!;
        var line = db.Model.FindEntityType(typeof(InventoryCountLine))!;
        Assert.Equal("inventory", count.GetSchema());
        Assert.True(count.FindProperty(nameof(InventoryCount.RowVersion))!.IsConcurrencyToken);
        Assert.Equal(DeleteBehavior.Restrict, count.GetForeignKeys().Single().DeleteBehavior);
        Assert.Contains(line.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(InventoryCountLine.InventoryCountId), nameof(InventoryCountLine.ProductVariantId)]));
    }

    [Fact]
    public void Completed_count_cannot_be_saved_without_matching_adjustment()
    {
        using var db = NewDb();
        var count = new InventoryCount(Guid.NewGuid(), "user");
        var variantId = Guid.NewGuid();
        count.AddLine(variantId, 17);
        db.InventoryCounts.Attach(count);
        count.SetCountedQuantity(variantId, 15);
        count.Complete("manager");
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    private static InventoryDbContext NewDb() => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
