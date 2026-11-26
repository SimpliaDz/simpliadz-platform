using Simplia.Api.Modules;
using Simplia.Inventory.Core.Entities.Catalog;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class InventoryDocumentTemplateTests
{
    [Fact]
    public void Template_rejects_unsupported_columns_and_renders_only_selected_columns()
    {
        Assert.Throws<ArgumentException>(() => new InventoryDocumentTemplate("Bad",
            InventoryDocumentType.CountSheet, "Count", ["Quantity"], "A4", false, null));
        var template = new InventoryDocumentTemplate("Count sheet",
            InventoryDocumentType.CountSheet, "Physical count", ["Product", "Sku", "Counted"],
            "A4", false, "Signed by <manager>");
        var html = InventoryDocumentRenderer.Render(template, "Location: Store",
            [new Dictionary<string, string> { ["Product"] = "<script>Shirt</script>",
                ["Sku"] = "TS-M", ["Counted"] = "", ["Expected"] = "10" }]);
        Assert.Contains("&lt;script&gt;Shirt&lt;/script&gt;", html);
        Assert.Contains("Signed by &lt;manager&gt;", html);
        Assert.DoesNotContain("<th>Expected</th>", html);
        Assert.Contains("<th>Counted</th>", html);
    }
}
