using Microsoft.EntityFrameworkCore;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Sales.Tests;

public sealed class PaymentTests
{
    [Fact]
    public void Payment_requires_positive_amount_valid_method_and_actor()
    {
        var saleId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() => new Payment(Guid.NewGuid(), saleId,
            0m, PaymentMethod.Cash, actor));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Payment(Guid.NewGuid(), saleId,
            1m, (PaymentMethod)99, actor));
        Assert.Throws<ArgumentException>(() => new Payment(Guid.NewGuid(), saleId,
            1m, PaymentMethod.Card, Guid.Empty));
    }

    [Fact]
    public void Completed_sale_tracks_partial_and_full_payment_without_overpayment()
    {
        var sale = new Sale(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid());
        sale.AddLine(Guid.NewGuid(), 1, 20m, 5m);
        Assert.Throws<InvalidOperationException>(() => sale.RecordPayment(1m));
        sale.Complete(Guid.NewGuid());
        sale.RecordPayment(12m);
        Assert.Equal(12m, sale.PaidAmount);
        Assert.Equal(8m, sale.BalanceDue);
        Assert.Throws<ArgumentOutOfRangeException>(() => sale.RecordPayment(9m));
        sale.RecordPayment(8m);
        Assert.Equal(0m, sale.BalanceDue);
    }

    [Fact]
    public void Payment_rows_are_immutable_and_paid_amount_needs_payment()
    {
        using var db = NewDb();
        var sale = new Sale(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid());
        sale.AddLine(Guid.NewGuid(), 1, 10m, 0m);
        sale.Complete(Guid.NewGuid());
        db.Sales.Attach(sale);
        sale.RecordPayment(5m);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Payment_model_has_restrictive_sale_link()
    {
        using var db = NewDb();
        var entity = db.Model.FindEntityType(typeof(Payment))!;
        Assert.Equal("sales", entity.GetSchema());
        Assert.Equal(2, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    private static SalesDbContext NewDb() => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
