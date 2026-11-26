using Microsoft.EntityFrameworkCore;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class StockMovementTests
{
    [Theory]
    [InlineData(StockMovementType.OpeningBalance, 5)]
    [InlineData(StockMovementType.PurchaseReceipt, 5)]
    [InlineData(StockMovementType.SaleReturn, 5)]
    [InlineData(StockMovementType.TransferIn, 5)]
    [InlineData(StockMovementType.Sale, -5)]
    [InlineData(StockMovementType.TransferOut, -5)]
    [InlineData(StockMovementType.Adjustment, -5)]
    public void Ledger_accepts_valid_signed_movements(StockMovementType type, int delta)
    {
        var actor = Guid.NewGuid().ToString();
        var documentId = Guid.NewGuid();
        var movement = new StockMovement(Guid.NewGuid(), Guid.NewGuid(), type, delta,
            actor, reason: "Counted", referenceType: "Document", referenceId: documentId);

        Assert.Equal(delta, movement.QuantityDelta);
        Assert.Equal(type, movement.MovementType);
        Assert.Equal(actor, movement.CreatedBy);
        Assert.Equal(documentId, movement.ReferenceId);
        Assert.True(movement.OccurredAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Ledger_rejects_wrong_sign_zero_and_incomplete_document_reference()
    {
        var variant = Guid.NewGuid();
        var location = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new StockMovement(variant, location,
            StockMovementType.Sale, 1, "user"));
        Assert.Throws<ArgumentException>(() => new StockMovement(variant, location,
            StockMovementType.PurchaseReceipt, -1, "user"));
        Assert.Throws<ArgumentException>(() => new StockMovement(variant, location,
            StockMovementType.Adjustment, 0, "user"));
        Assert.Throws<ArgumentException>(() => new StockMovement(variant, location,
            StockMovementType.PurchaseReceipt, 1, "user", referenceId: Guid.NewGuid()));
    }

    [Fact]
    public void Existing_ledger_columns_are_preserved_and_updates_are_blocked()
    {
        using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var entity = db.Model.FindEntityType(typeof(StockMovement))!;
        var table = entity.GetTableName()!;
        var schema = entity.GetSchema();
        var identifier = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, schema);
        Assert.Equal("Type", entity.FindProperty(nameof(StockMovement.MovementType))!.GetColumnName(identifier));
        Assert.Equal("Note", entity.FindProperty(nameof(StockMovement.Reason))!.GetColumnName(identifier));
        Assert.Equal("SourceDocumentType", entity.FindProperty(nameof(StockMovement.ReferenceType))!.GetColumnName(identifier));

        var movement = new StockMovement(Guid.NewGuid(), Guid.NewGuid(),
            StockMovementType.OpeningBalance, 1, "system");
        db.StockMovements.Attach(movement);
        db.Entry(movement).State = EntityState.Modified;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.Entry(movement).State = EntityState.Deleted;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Balance_change_without_matching_movement_is_blocked_before_database_write()
    {
        using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var balance = new StockBalance(Guid.NewGuid(), Guid.NewGuid());
        db.StockBalances.Attach(balance);
        balance.ApplyDelta(3);

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
}
