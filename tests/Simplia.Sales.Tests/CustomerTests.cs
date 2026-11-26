using Microsoft.EntityFrameworkCore;
using Simplia.Sales.Core.Entities;
using Simplia.Sales.Infrastructure.Data;
using Xunit;

namespace Simplia.Sales.Tests;

public sealed class CustomerTests
{
    [Fact]
    public void Customer_normalizes_details_and_can_change_type()
    {
        var customer = new Customer("  Samira  ", null, " samira@example.com ",
            " Algiers ", CustomerType.Retail, " Regular ");
        Assert.Equal("Samira", customer.Name);
        Assert.Null(customer.Phone);
        Assert.Equal("samira@example.com", customer.Email);
        Assert.Equal(CustomerType.Retail, customer.CustomerType);

        customer.Update("Samira Business", " 0550 123 456 ", null, null,
            CustomerType.Wholesale, null);
        Assert.Equal("Samira Business", customer.Name);
        Assert.Equal("0550 123 456", customer.Phone);
        Assert.Equal(CustomerType.Wholesale, customer.CustomerType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string name) =>
        Assert.Throws<ArgumentException>(() => new Customer(name, null, null, null,
            CustomerType.Retail, null));

    [Fact]
    public void Invalid_update_does_not_change_customer()
    {
        var customer = new Customer("Original", null, "valid@example.com", null,
            CustomerType.Retail, null);
        Assert.Throws<ArgumentException>(() => customer.Update("Changed", null,
            "not-an-email", null, CustomerType.Wholesale, null));
        Assert.Equal("Original", customer.Name);
        Assert.Equal(CustomerType.Retail, customer.CustomerType);
    }

    [Fact]
    public void Unknown_customer_type_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Customer("Customer", null,
            null, null, (CustomerType)99, null));
    }

    [Fact]
    public void Deactivation_keeps_identity_and_contact_history()
    {
        var customer = new Customer("Customer", "123", null, null,
            CustomerType.Wholesale, null);
        var id = customer.Id;
        customer.Deactivate();
        Assert.Equal(id, customer.Id);
        Assert.Equal("123", customer.Phone);
        Assert.False(customer.IsActive);
    }

    [Fact]
    public void Customer_model_uses_sales_schema_and_concurrency_token()
    {
        using var db = NewDb();
        var entity = db.Model.FindEntityType(typeof(Customer))!;
        Assert.Equal("sales", entity.GetSchema());
        Assert.Equal("Customers", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(Customer.RowVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void Hard_deleting_customer_is_blocked()
    {
        using var db = NewDb();
        var customer = new Customer("Historical customer", null, null, null,
            CustomerType.Retail, null);
        db.Customers.Attach(customer);
        db.Customers.Remove(customer);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    private static SalesDbContext NewDb() => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
