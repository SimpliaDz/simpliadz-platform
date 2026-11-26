using Microsoft.EntityFrameworkCore;
using Simplia.Purchasing.Core.Entities;
using Simplia.Purchasing.Infrastructure.Data;
using Xunit;

namespace Simplia.Purchasing.Tests;

public sealed class PurchaseOrderTests
{
    [Fact]
    public void Supplier_payments_require_confirmed_orders_and_cannot_exceed_balance()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), "buyer");
        order.AddLine(Guid.NewGuid(), 3, 2000m);
        Assert.Throws<InvalidOperationException>(() => order.ValidateSupplierPayment(2000m, 0m));
        order.PlaceOrder();

        order.ValidateSupplierPayment(2000m, 0m);
        Assert.Equal(4000m, order.Total - 2000m);
        Assert.Throws<InvalidOperationException>(() => order.ValidateSupplierPayment(4000.01m, 2000m));
        order.ValidateSupplierPayment(4000m, 2000m);
        Assert.Throws<InvalidOperationException>(() => order.ValidateSupplierPayment(1m, order.Total));
    }

    [Fact]
    public void Draft_lines_calculate_total_without_receiving_stock()
    {
        var order = NewOrder();
        order.AddLine(Guid.NewGuid(), 3, 125.50m);
        order.AddLine(Guid.NewGuid(), 2, 10m);

        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Equal(396.50m, order.Total);
        Assert.All(order.Lines, line => Assert.Equal(0, line.ReceivedQuantity));

        order.PlaceOrder();
        Assert.Equal(PurchaseOrderStatus.Ordered, order.Status);
        Assert.All(order.Lines, line => Assert.Equal(0, line.ReceivedQuantity));
    }

    [Fact]
    public void Receipt_progress_updates_quantities_and_status()
    {
        var order = NewOrder();
        var first = order.AddLine(Guid.NewGuid(), 3, 10m);
        var second = order.AddLine(Guid.NewGuid(), 2, 20m);
        order.PlaceOrder();

        order.RecordReceipt(first.Id, 1);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(1, first.ReceivedQuantity);
        Assert.Throws<InvalidOperationException>(() => order.RecordReceipt(first.Id, 3));

        order.RecordReceipt(first.Id, 2);
        order.RecordReceipt(second.Id, 2);
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(2, second.ReceivedQuantity);
        Assert.Throws<InvalidOperationException>(() => order.RecordReceipt(second.Id, 1));
    }

    [Fact]
    public void Cancelled_order_cannot_receive_or_change_lines()
    {
        var order = NewOrder();
        var line = order.AddLine(Guid.NewGuid(), 1, 0m);
        order.PlaceOrder();
        order.Cancel();
        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.RecordReceipt(line.Id, 1));
        Assert.Throws<InvalidOperationException>(() => order.AddLine(Guid.NewGuid(), 1, 1m));
    }

    [Fact]
    public void Invalid_references_and_line_values_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new PurchaseOrder(Guid.Empty, Guid.NewGuid(), "actor"));
        Assert.Throws<ArgumentException>(() => new PurchaseOrder(Guid.NewGuid(), Guid.Empty, "actor"));
        var order = NewOrder();
        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddLine(Guid.NewGuid(), 0, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddLine(Guid.NewGuid(), 1, -1m));
        var variant = Guid.NewGuid();
        order.AddLine(variant, 1, 1m);
        Assert.Throws<InvalidOperationException>(() => order.AddLine(variant, 1, 1m));
        Assert.Single(order.Lines);
    }

    [Fact]
    public void Purchasing_model_has_restrictive_supplier_link_and_unique_line_variant()
    {
        using var db = new PurchasingDbContext(new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var order = db.Model.FindEntityType(typeof(PurchaseOrder))!;
        var line = db.Model.FindEntityType(typeof(PurchaseOrderLine))!;
        Assert.Equal("purchasing", order.GetSchema());
        Assert.Equal(DeleteBehavior.Restrict, order.GetForeignKeys().Single().DeleteBehavior);
        Assert.True(order.FindProperty(nameof(PurchaseOrder.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(line.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(PurchaseOrderLine.PurchaseOrderId), nameof(PurchaseOrderLine.ProductVariantId)]));
    }

    private static PurchaseOrder NewOrder() => new(Guid.NewGuid(), Guid.NewGuid(), "actor");
}
