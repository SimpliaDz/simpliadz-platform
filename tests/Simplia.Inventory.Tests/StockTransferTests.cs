using Microsoft.EntityFrameworkCore;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class StockTransferTests
{
    [Fact]
    public void Draft_accepts_multiple_distinct_positive_lines_without_touching_stock()
    {
        using var db = CreateContext();
        var transfer = new StockTransfer(Guid.NewGuid(), Guid.NewGuid(), "creator");
        var firstVariant = Guid.NewGuid();
        var secondVariant = Guid.NewGuid();
        db.StockTransfers.Add(transfer);
        var first = transfer.AddLine(firstVariant, 3);
        transfer.AddLine(secondVariant, 5);
        db.ChangeTracker.DetectChanges();

        Assert.Equal(StockTransferStatus.Draft, transfer.Status);
        Assert.Equal(2, transfer.Lines.Count);
        Assert.Equal(transfer.Id, first.StockTransferId);
        Assert.Equal(2, transfer.Revision);
        Assert.Empty(db.ChangeTracker.Entries<StockBalance>());
        Assert.Empty(db.ChangeTracker.Entries<StockMovement>());
        Assert.Equal(2, db.ChangeTracker.Entries<StockTransferLine>().Count());
    }

    [Fact]
    public void Invalid_locations_quantities_and_duplicate_variants_are_rejected()
    {
        var location = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new StockTransfer(location, location, "creator"));
        Assert.Throws<ArgumentException>(() => new StockTransfer(Guid.Empty, location, "creator"));
        var transfer = new StockTransfer(location, Guid.NewGuid(), "creator");
        Assert.Throws<ArgumentOutOfRangeException>(() => transfer.AddLine(Guid.NewGuid(), 0));
        var variant = Guid.NewGuid();
        transfer.AddLine(variant, 1);
        Assert.Throws<InvalidOperationException>(() => transfer.AddLine(variant, 2));
    }

    [Fact]
    public void Completion_is_once_only_and_prevents_draft_edits()
    {
        var transfer = new StockTransfer(Guid.NewGuid(), Guid.NewGuid(), "creator");
        Assert.Throws<InvalidOperationException>(() => transfer.Complete("operator"));
        transfer.AddLine(Guid.NewGuid(), 2);
        transfer.Complete("operator");

        Assert.Equal(StockTransferStatus.Completed, transfer.Status);
        Assert.Equal("operator", transfer.CompletedBy);
        Assert.NotNull(transfer.CompletedAt);
        Assert.Throws<InvalidOperationException>(() => transfer.Complete("operator"));
        Assert.Throws<InvalidOperationException>(() => transfer.AddLine(Guid.NewGuid(), 1));
        Assert.Throws<InvalidOperationException>(() => transfer.Cancel());
    }

    [Fact]
    public void Cancelled_transfer_cannot_be_completed()
    {
        var transfer = new StockTransfer(Guid.NewGuid(), Guid.NewGuid(), "creator");
        transfer.Cancel();
        Assert.Equal(StockTransferStatus.Cancelled, transfer.Status);
        Assert.Throws<InvalidOperationException>(() => transfer.Complete("operator"));
    }

    [Fact]
    public void Database_model_enforces_unique_number_line_and_concurrency()
    {
        using var db = CreateContext();
        var transfer = db.Model.FindEntityType(typeof(StockTransfer))!;
        var line = db.Model.FindEntityType(typeof(StockTransferLine))!;
        Assert.Contains(transfer.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual([nameof(StockTransfer.TransferNumber)]));
        Assert.True(transfer.FindProperty(nameof(StockTransfer.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(line.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(StockTransferLine.StockTransferId), nameof(StockTransferLine.ProductVariantId)]));
    }

    [Fact]
    public void Completed_status_cannot_be_saved_without_paired_movements()
    {
        using var db = CreateContext();
        var transfer = new StockTransfer(Guid.NewGuid(), Guid.NewGuid(), "creator");
        transfer.AddLine(Guid.NewGuid(), 2);
        db.StockTransfers.Attach(transfer);
        transfer.Complete("operator");

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    private static InventoryDbContext CreateContext() => new(
        new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
