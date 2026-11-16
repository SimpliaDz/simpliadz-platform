using SimpliaDz.Api.Modules;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

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
}
