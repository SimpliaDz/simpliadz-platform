using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Sales.Tests;

public sealed class SaleLineDeleteGuardTests
{
    [Fact]
    public async Task Allows_removing_a_line_as_part_of_a_draft_edit()
    {
        await using var db = CreateContext();
        var sale = CreateSale();
        var line = sale.Lines.Single();
        db.Attach(sale);
        db.Entry(line).State = EntityState.Deleted;
        sale.RemoveLine(line);

        Assert.Equal(1, await db.SaveChangesAsync());
    }

    [Fact]
    public async Task Rejects_deleting_a_completed_sale_line()
    {
        await using var db = CreateContext();
        var sale = CreateSale();
        sale.Complete(Guid.NewGuid());
        db.Attach(sale);
        db.Entry(sale.Lines.Single()).State = EntityState.Deleted;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("Sale history cannot be deleted.", error.Message);
    }

    private static Sale CreateSale()
    {
        var sale = new Sale(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid());
        sale.AddLine(Guid.NewGuid(), 1, 100m, 0m, 0m);
        return sale;
    }

    private static SalesDbContext CreateContext() => new(new DbContextOptionsBuilder<SalesDbContext>()
        .UseSqlServer("Server=unused;Database=unused;Integrated Security=true")
        .AddInterceptors(new SuppressSave()).Options);

    private sealed class SuppressSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
    }
}
