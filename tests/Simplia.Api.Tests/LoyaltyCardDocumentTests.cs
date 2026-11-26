using QuestPDF.Infrastructure;
using Simplia.Api.Modules;
using Xunit;

namespace Simplia.Api.Tests;

public sealed class LoyaltyCardDocumentTests
{
    [Fact]
    public void Renders_a_pdf_with_a_long_customer_name()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = LoyaltyCardDocument.Render(new string('A', 150), Guid.NewGuid(), "FID-123456");
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public void Artwork_contains_escaped_organization_name_and_barcode()
    {
        var svg = LoyaltyCardDocument.Artwork("Manel", Guid.NewGuid(), "FID-123456", "Maison & Jardin");
        Assert.Contains("Maison &amp; Jardin", svg);
        Assert.Contains("height='60'", svg);
    }
}
