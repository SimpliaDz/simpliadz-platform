using SimpliaDz.Inventory.Core.Entities.Catalog;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class VariantTests
{
    [Fact]
    public void A_product_can_have_multiple_variants_with_distinct_combinations()
    {
        var product = new Product("Classic T-Shirt");
        var black = Guid.NewGuid();
        var medium = Guid.NewGuid();
        var large = Guid.NewGuid();
        var first = product.AddVariant("TS-BLK-M", 1000, barcode: "12345",
            attributeValueIds: [black, medium]);
        var second = product.AddVariant("TS-BLK-L", 1000,
            attributeValueIds: [black, large]);

        Assert.Equal(product.Id, first.ProductId);
        Assert.Equal(2, first.AttributeValues.Count);
        Assert.NotEqual(first.CombinationKey, second.CombinationKey);
        Assert.Equal(2, product.Variants.Count);
        Assert.Equal("12345", first.Barcode);
    }

    [Fact]
    public void Combination_is_independent_of_value_order_and_must_be_unique_per_product()
    {
        var product = new Product("Classic T-Shirt");
        var black = Guid.NewGuid();
        var medium = Guid.NewGuid();
        var first = product.AddVariant("TS-BLK-M", 1000, attributeValueIds: [black, medium]);

        Assert.Throws<InvalidOperationException>(() => product.AddVariant(
            "TS-M-BLK", 1000, attributeValueIds: [medium, black]));
        Assert.Single(product.Variants);
        Assert.Equal(first.CombinationKey, new ProductVariant(product.Id,
            new SimpliaDz.Inventory.Core.ValueObjects.Sku("TEMP"), 1000)
            .WithValues(medium, black).CombinationKey);
    }

    [Fact]
    public void Duplicate_value_ids_are_rejected()
    {
        var product = new Product("Shirt");
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => product.AddVariant("SHIRT-1", 1000,
            attributeValueIds: [id, id]));
    }

    [Fact]
    public void Generated_barcode_is_unique_to_variant_and_does_not_replace_factory_barcode()
    {
        var product = new Product("Shirt");
        var first = product.AddVariant("SHIRT-S", 100m);
        var second = product.AddVariant("SHIRT-M", 100m);
        var firstBarcode = first.GenerateInternalBarcode();
        Assert.StartsWith("SDZ", firstBarcode);
        Assert.Equal(35, firstBarcode.Length);
        Assert.NotEqual(firstBarcode, second.GenerateInternalBarcode());
        Assert.Throws<InvalidOperationException>(() => first.GenerateInternalBarcode());

        var factory = product.AddVariant("SHIRT-L", 100m, barcode: "FACTORY-123");
        Assert.Throws<InvalidOperationException>(() => factory.GenerateInternalBarcode());
        Assert.Equal("FACTORY-123", factory.Barcode);
        var inactive = product.AddVariant("SHIRT-XL", 100m);
        inactive.Deactivate();
        Assert.Throws<InvalidOperationException>(() => inactive.GenerateInternalBarcode());
    }
}

internal static class VariantTestExtensions
{
    public static ProductVariant WithValues(this ProductVariant variant, params Guid[] ids)
    {
        variant.AssignAttributeValues(ids);
        return variant;
    }
}
