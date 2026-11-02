using Microsoft.EntityFrameworkCore;
using SimpliaDz.Purchasing.Core.Entities;
using SimpliaDz.Purchasing.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Purchasing.Tests;

public sealed class GoodsReceiptTests
{
    [Fact]
    public void Receipt_captures_positive_lines_and_cost_snapshot()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), "buyer");
        var line = po.AddLine(Guid.NewGuid(), 100, 12.5m);
        po.PlaceOrder();
        var id = Guid.NewGuid();
        var receipt = new GoodsReceipt(id, po.Id, po.DestinationLocationId, "receiver",
            [new GoodsReceiptLineItem(line.Id, line.ProductVariantId, 70, line.UnitCost)]);
        po.RecordReceipt(line.Id, 70);

        Assert.Equal(id, receipt.Id);
        Assert.Equal(po.Id, receipt.PurchaseOrderId);
        Assert.Equal(po.DestinationLocationId, receipt.LocationId);
        Assert.Equal(70, receipt.Lines.Single().QuantityReceived);
        Assert.Equal(12.5m, receipt.Lines.Single().UnitCost);
        Assert.Equal(70, line.ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, po.Status);

        var last = new GoodsReceipt(Guid.NewGuid(), po.Id, po.DestinationLocationId,
            "receiver", [new GoodsReceiptLineItem(line.Id, line.ProductVariantId, 30, line.UnitCost)]);
        po.RecordReceipt(last.Lines.Single().PurchaseOrderLineId, 30);
        Assert.Equal(PurchaseOrderStatus.Received, po.Status);
        Assert.Throws<InvalidOperationException>(() => po.RecordReceipt(line.Id, 1));
    }

    [Fact]
    public void Receipt_rejects_invalid_or_repeated_lines()
    {
        var poId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new GoodsReceipt(Guid.Empty, poId, locationId,
            "receiver", [new GoodsReceiptLineItem(lineId, variantId, 1, 1m)]));
        Assert.Throws<ArgumentException>(() => new GoodsReceipt(Guid.NewGuid(), poId, locationId,
            "receiver", [new GoodsReceiptLineItem(lineId, variantId, 0, 1m)]));
        Assert.Throws<ArgumentException>(() => new GoodsReceipt(Guid.NewGuid(), poId, locationId,
            "receiver", [new GoodsReceiptLineItem(lineId, variantId, 1, 1m),
                new GoodsReceiptLineItem(lineId, variantId, 1, 1m)]));
    }

    [Fact]
    public void Receipt_model_has_restrictive_links_and_unique_order_line_per_receipt()
    {
        using var db = new PurchasingDbContext(new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var receipt = db.Model.FindEntityType(typeof(GoodsReceipt))!;
        var line = db.Model.FindEntityType(typeof(GoodsReceiptLine))!;
        Assert.Equal("purchasing", receipt.GetSchema());
        Assert.Equal(DeleteBehavior.Restrict, receipt.GetForeignKeys().Single().DeleteBehavior);
        Assert.All(line.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(line.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(GoodsReceiptLine.GoodsReceiptId), nameof(GoodsReceiptLine.PurchaseOrderLineId)]));
    }

    [Fact]
    public void Posted_receipts_cannot_be_deleted_or_modified()
    {
        using var db = new PurchasingDbContext(new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var receipt = new GoodsReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "receiver",
            [new GoodsReceiptLineItem(Guid.NewGuid(), Guid.NewGuid(), 1, 1m)]);
        db.GoodsReceipts.Attach(receipt);
        Assert.Throws<InvalidOperationException>(() =>
        {
            db.GoodsReceipts.Remove(receipt);
            db.SaveChanges();
        });
    }
}
