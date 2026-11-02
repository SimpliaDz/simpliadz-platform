using Microsoft.EntityFrameworkCore;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Sales.Tests;

public sealed class SaleTests
{
    [Fact]
    public void Draft_totals_and_snapshots_are_calculated_server_side()
    {
        var sale = NewSale();
        var line = sale.AddLine(Guid.NewGuid(), 2, 12m, 6.5m, 1m);
        Assert.Null(sale.SaleNumber);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(24m, sale.Subtotal);
        Assert.Equal(23m, line.LineTotal);
        Assert.Equal(6.5m, line.UnitCostSnapshot);
        sale.SetDiscount(2m);
        Assert.Equal(21m, sale.Total);
    }

    [Fact]
    public void Completion_assigns_number_once_and_cannot_be_repeated()
    {
        var sale = NewSale();
        sale.AddLine(Guid.NewGuid(), 1, 10m, 3m);
        sale.Complete(Guid.NewGuid());
        Assert.StartsWith("SALE-", sale.SaleNumber);
        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Throws<InvalidOperationException>(() => sale.Complete(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => sale.AddLine(Guid.NewGuid(), 1, 1m, 0m));
    }

    [Fact]
    public void Invalid_lines_and_discounts_do_not_mutate_sale()
    {
        var sale = NewSale();
        Assert.Throws<ArgumentOutOfRangeException>(() => sale.AddLine(Guid.NewGuid(), 0, 10m, 2m));
        Assert.Throws<ArgumentOutOfRangeException>(() => sale.AddLine(Guid.NewGuid(), 1, -1m, 2m));
        Assert.Throws<ArgumentException>(() => sale.AddLine(Guid.NewGuid(), 1, 10m, 2m, 11m));
        Assert.Throws<ArgumentOutOfRangeException>(() => sale.AddLine(Guid.NewGuid(), int.MaxValue,
            9999999999999999.99m, 0m));
        Assert.Empty(sale.Lines);
        var variantId = Guid.NewGuid();
        sale.AddLine(variantId, 1, 10m, 2m);
        Assert.Throws<InvalidOperationException>(() => sale.AddLine(variantId, 1, 10m, 2m));
        Assert.Throws<ArgumentException>(() => sale.SetDiscount(11m));
        Assert.Equal(10m, sale.Total);
    }

    [Fact]
    public void Cancelled_sale_cannot_be_completed()
    {
        var sale = NewSale();
        sale.AddLine(Guid.NewGuid(), 1, 10m, 2m);
        sale.Cancel();
        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Throws<InvalidOperationException>(() => sale.Complete(Guid.NewGuid()));
    }

    [Fact]
    public void Model_keeps_customer_history_and_unique_completed_number()
    {
        using var db = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var entity = db.Model.FindEntityType(typeof(Sale))!;
        Assert.Equal("sales", entity.GetSchema());
        Assert.Equal(DeleteBehavior.Restrict, entity.GetForeignKeys().Single().DeleteBehavior);
        Assert.True(entity.FindProperty(nameof(Sale.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.Properties.Single().Name == nameof(Sale.SaleNumber));
    }

    private static Sale NewSale() => new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid());
}
