using Microsoft.EntityFrameworkCore;
using SimpliaDz.Accounting.Core;
using SimpliaDz.Accounting.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Accounting.Tests;

public sealed class ExpenseTests
{
    [Fact]
    public void Expense_keeps_creator_when_updated_and_allows_optional_location()
    {
        var actor = Guid.NewGuid();
        var category = Guid.NewGuid();
        var expense = new Expense(category, null, 250.25m,
            new DateOnly(2026, 11, 2), "  Office supplies  ", actor);
        Assert.Null(expense.LocationId);
        Assert.Equal("Office supplies", expense.Description);
        expense.Update(category, Guid.NewGuid(), 300m,
            new DateOnly(2026, 11, 3), null);
        Assert.Equal(actor, expense.CreatedBy);
        Assert.Equal(300m, expense.Amount);
        Assert.Null(expense.Description);
    }

    [Fact]
    public void Expense_rejects_missing_category_nonpositive_amount_and_missing_date()
    {
        var category = Guid.NewGuid();
        var actor = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Expense(Guid.Empty, null, 10m,
            new DateOnly(2026, 11, 2), null, actor));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Expense(category, null, 0m,
            new DateOnly(2026, 11, 2), null, actor));
        Assert.Throws<ArgumentException>(() => new Expense(category, null, 10m,
            default, null, actor));
    }

    [Fact]
    public void Invalid_update_does_not_change_existing_expense()
    {
        var category = Guid.NewGuid();
        var expense = new Expense(category, null, 10m,
            new DateOnly(2026, 11, 2), "Original", Guid.NewGuid());
        Assert.Throws<ArgumentOutOfRangeException>(() => expense.Update(category,
            null, -1m, new DateOnly(2026, 11, 3), "Changed"));
        Assert.Equal(10m, expense.Amount);
        Assert.Equal("Original", expense.Description);
    }

    [Fact]
    public void Expense_model_restricts_category_deletion_and_blocks_expense_deletion()
    {
        using var db = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var model = db.Model.FindEntityType(typeof(Expense))!;
        Assert.Equal("accounting", model.GetSchema());
        Assert.Equal(DeleteBehavior.Restrict, model.GetForeignKeys().Single().DeleteBehavior);
        var expense = new Expense(Guid.NewGuid(), null, 10m,
            new DateOnly(2026, 11, 2), null, Guid.NewGuid());
        db.Expenses.Attach(expense);
        db.Expenses.Remove(expense);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
}
