using Simplia.Api.Modules;
using Simplia.Inventory.Core.Entities.Catalog;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class BarcodeLabelTemplateTests
{
    [Fact]
    public void Template_validates_dimensions_and_renders_code128_across_pages()
    {
        Assert.Throws<ArgumentException>(() => new BarcodeLabelTemplate("Invalid", 50, 50,
            90, 40, 5, 3, true, true));
        var template = new BarcodeLabelTemplate("A4 labels", 210, 297,
            90, 40, 5, 3, true, true);
        var item = new BarcodeLabelItem("<script>Shirt</script>", "TS-S",
            "SDZ1234567890ABCDEF1234567890ABCDEF");
        var html = BarcodeLabelDocumentRenderer.Render(template,
            Enumerable.Repeat(item, 13).ToArray());
        Assert.Equal(2, html.Split("<svg ").Length - 1);
        Assert.Contains("<rect", html);
        Assert.Contains("&lt;script&gt;Shirt&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void Price_is_optional_and_fits_the_smallest_label_with_name_and_sku()
    {
        var template = new BarcodeLabelTemplate("Small", 50, 50, 40, 25, 1, 1, true, true);
        var label = new BarcodeLabelItem("A long product name for a narrow label", "ABC-M",
            InternalBarcode.Generate(), "1 234,50 DZD");
        var priced = BarcodeLabelDocumentRenderer.Render(template, [label, label]);
        var unpriced = BarcodeLabelDocumentRenderer.Render(template, [label with { Price = null }]);

        Assert.Equal(2, priced.Split("1 234,50 DZD").Length - 1);
        Assert.Contains("height=\"6\"", priced);
        Assert.Contains("textLength=\"36\"", priced);
        Assert.DoesNotContain("DZD", unpriced);
        Assert.Contains(label.Barcode, unpriced);
    }

    [Fact]
    public void Generated_ean13_barcode_can_be_printed()
    {
        var template = new BarcodeLabelTemplate("A4 labels", 210, 297,
            90, 40, 5, 3, true, true);
        var barcode = InternalBarcode.Generate();

        var html = BarcodeLabelDocumentRenderer.Render(template,
            [new BarcodeLabelItem("Shirt", "TS-M", barcode)]);

        Assert.True(InternalBarcode.IsValidEan13(barcode));
        Assert.Contains(barcode, html);
        Assert.Contains("<rect", html);
    }
}
