using Microsoft.EntityFrameworkCore;
using SimpliaDz.Accounting.Core;
using SimpliaDz.Accounting.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Accounting.Tests;

public sealed class ExpenseCategoryTests
{
    [Fact]
    public void Category_can_be_renamed_and_deactivated_without_losing_identity()
    {
        var category = new ExpenseCategory("  Rent  ");
        var id = category.Id;
        Assert.Equal("Rent", category.Name);
        Assert.True(category.IsActive);
        category.Update("  Utilities  ");
        category.Deactivate();
        Assert.Equal(id, category.Id);
        Assert.Equal("Utilities", category.Name);
        Assert.False(category.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Category_name_is_required(string name)
    {
        Assert.Throws<ArgumentException>(() => new ExpenseCategory(name));
    }

    [Fact]
    public void Invalid_update_preserves_previous_name()
    {
        var category = new ExpenseCategory("Rent");
        Assert.Throws<ArgumentException>(() => category.Update(" "));
        Assert.Equal("Rent", category.Name);
    }

    [Fact]
    public void Model_uses_accounting_schema_and_blocks_hard_delete()
    {
        using var db = NewDb();
        var model = db.Model.FindEntityType(typeof(ExpenseCategory))!;
        Assert.Equal("accounting", model.GetSchema());
        Assert.Equal("ExpenseCategories", model.GetTableName());
        Assert.True(model.FindProperty(nameof(ExpenseCategory.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(model.GetIndexes(), x => x.IsUnique &&
            x.Properties.Single().Name == nameof(ExpenseCategory.Name));
        var category = new ExpenseCategory("Historical category");
        db.ExpenseCategories.Attach(category);
        db.ExpenseCategories.Remove(category);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    private static AccountingDbContext NewDb() => new(
        new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
