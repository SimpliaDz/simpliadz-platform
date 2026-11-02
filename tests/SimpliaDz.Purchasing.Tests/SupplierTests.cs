using Microsoft.EntityFrameworkCore;
using SimpliaDz.Purchasing.Core.Entities;
using SimpliaDz.Purchasing.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Purchasing.Tests;

public sealed class SupplierTests
{
    [Fact]
    public void Supplier_normalizes_contact_details_and_can_be_updated()
    {
        var supplier = new Supplier("  Textile House  ", " 0550 12 34 56 ",
            " team@example.com ", " Algiers ", " Main supplier ");
        var id = supplier.Id;
        Assert.Equal("Textile House", supplier.Name);
        Assert.Equal("team@example.com", supplier.Email);
        Assert.Equal("0550 12 34 56", supplier.Phone);
        Assert.True(supplier.IsActive);

        supplier.Update("New Name", null, null, "Oran", "Updated notes");
        Assert.Equal(id, supplier.Id);
        Assert.Equal("New Name", supplier.Name);
        Assert.Null(supplier.Email);
        Assert.Equal("Oran", supplier.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Supplier_name_is_required(string name)
    {
        Assert.Throws<ArgumentException>(() => new Supplier(name));
    }

    [Fact]
    public void Invalid_update_does_not_change_existing_supplier()
    {
        var supplier = new Supplier("Original", email: "valid@example.com");
        Assert.Throws<ArgumentException>(() => supplier.Update("Changed", null,
            "invalid-email", null, null));
        Assert.Equal("Original", supplier.Name);
        Assert.Equal("valid@example.com", supplier.Email);
    }

    [Fact]
    public void Deactivation_retains_identity_and_contact_history()
    {
        var supplier = new Supplier("Supplier", email: "supplier@example.com");
        var id = supplier.Id;
        supplier.Deactivate();
        Assert.False(supplier.IsActive);
        Assert.Equal(id, supplier.Id);
        Assert.Equal("supplier@example.com", supplier.Email);
    }

    [Fact]
    public void Supplier_is_tenant_database_entity_with_concurrency_token()
    {
        using var db = new PurchasingDbContext(new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var entity = db.Model.FindEntityType(typeof(Supplier))!;
        Assert.Equal("purchasing", entity.GetSchema());
        Assert.Equal("Suppliers", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(Supplier.RowVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void Hard_deleting_supplier_is_blocked_before_database_write()
    {
        using var db = new PurchasingDbContext(new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var supplier = new Supplier("Historical supplier");
        db.Suppliers.Attach(supplier);
        db.Suppliers.Remove(supplier);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
}
