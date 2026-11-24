using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Organization.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;

namespace SimpliaDz.Api.Modules;

public sealed record GeneratedPdf(string FileName, byte[] Content);

/// <summary>Generates sales documents from recorded sales snapshots, never current catalogue prices.</summary>
public sealed class SalesPdfDocumentService(SalesDbContext sales, InventoryDbContext inventory,
    OrganizationDbContext organization)
{
    public async Task<GeneratedPdf> ProFormaAsync(Guid saleId, CancellationToken ct)
    {
        var model = await LoadAsync(saleId, ct);
        return new($"pro-forma-{model.DocumentNumber}.pdf", BuildA4(model, "PRO FORMA INVOICE"));
    }

    public async Task<GeneratedPdf> ReceiptAsync(Guid saleId, CancellationToken ct)
    {
        var model = await LoadAsync(saleId, ct);
        if (model.Status is not ("Completed" or "PartiallyReturned" or "Returned"))
            throw new ConflictException("A receipt can only be generated for a completed sale.");
        return new($"receipt-{model.DocumentNumber}.pdf", BuildReceipt(model));
    }

    private async Task<SalesDocumentModel> LoadAsync(Guid saleId, CancellationToken ct)
    {
        var sale = await sales.Sales.AsNoTracking().Include(x => x.Customer).Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == saleId, ct) ?? throw new NotFoundException("Sale", saleId);
        var variants = await inventory.ProductVariants.AsNoTracking().Include(x => x.Product)
            .Where(x => sale.Lines.Select(line => line.ProductVariantId).Contains(x.Id))
            .Select(x => new { x.Id, Sku = x.Sku.Value, ProductName = x.Product.Name })
            .ToDictionaryAsync(x => x.Id, ct);
        var location = await inventory.Locations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sale.LocationId, ct);
        var profile = await organization.Organizations.AsNoTracking().SingleOrDefaultAsync(ct);
        return new SalesDocumentModel(
            profile?.Name ?? "SimpliaDz", profile?.LegalName, profile?.Address, profile?.Phone,
            profile?.Email, profile?.CurrencyCode ?? "DZD", location?.Name ?? "Location", location?.Address,
            sale.Customer?.Name ?? "Walk-in customer", sale.Customer?.Phone, sale.Customer?.Address,
            sale.SaleNumber ?? $"PF-{sale.Id.ToString("N")[..10].ToUpperInvariant()}", sale.Status.ToString(),
            sale.SaleDate, sale.Subtotal, sale.DiscountAmount, sale.Total, sale.PaidAmount,
            sale.Lines.Select(line => new SalesDocumentLine(
                variants.GetValueOrDefault(line.ProductVariantId)?.ProductName ?? "Product",
                variants.GetValueOrDefault(line.ProductVariantId)?.Sku ?? line.ProductVariantId.ToString(),
                line.Quantity, line.UnitPrice, line.DiscountAmount, line.LineTotal)).ToList());
    }

    private static byte[] BuildA4(SalesDocumentModel model, string title) => Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(38); page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Row(row =>
            {
                row.RelativeItem().Column(c => { c.Item().Text(model.OrganizationName).Bold().FontSize(20).FontColor(Colors.Blue.Darken2); if (!string.IsNullOrWhiteSpace(model.OrganizationLegalName)) c.Item().Text(model.OrganizationLegalName!); if (!string.IsNullOrWhiteSpace(model.OrganizationAddress)) c.Item().Text(model.OrganizationAddress!); });
                row.ConstantItem(180).AlignRight().Column(c => { c.Item().Text(title).Bold().FontSize(16); c.Item().Text(model.DocumentNumber).Bold().FontSize(12); c.Item().Text(model.Date.ToString("yyyy-MM-dd HH:mm")); });
            });
            page.Content().PaddingVertical(22).Column(column =>
            {
                column.Spacing(12);
                column.Item().Row(row => { row.RelativeItem().Element(Card).Column(c => { c.Item().Text("BILL TO").SemiBold().FontColor(Colors.Grey.Darken1); c.Item().Text(model.CustomerName).Bold(); if (model.CustomerPhone is not null) c.Item().Text(model.CustomerPhone); if (model.CustomerAddress is not null) c.Item().Text(model.CustomerAddress); }); row.RelativeItem().PaddingLeft(12).Element(Card).Column(c => { c.Item().Text("ISSUED FROM").SemiBold().FontColor(Colors.Grey.Darken1); c.Item().Text(model.LocationName).Bold(); if (model.LocationAddress is not null) c.Item().Text(model.LocationAddress); }); });
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(2); c.ConstantColumn(45); c.ConstantColumn(75); c.ConstantColumn(85); });
                    table.Header(h => { Header(h.Cell(), "Product"); Header(h.Cell(), "SKU"); Header(h.Cell().AlignRight(), "Qty"); Header(h.Cell().AlignRight(), "Unit price"); Header(h.Cell().AlignRight(), "Total"); });
                    foreach (var line in model.Lines) { Cell(table.Cell(), line.ProductName); Cell(table.Cell(), line.Sku); Cell(table.Cell().AlignRight(), line.Quantity.ToString()); Cell(table.Cell().AlignRight(), Money(line.UnitPrice, model.Currency)); Cell(table.Cell().AlignRight(), Money(line.LineTotal, model.Currency)); }
                });
                column.Item().AlignRight().Width(260).Column(c => { Total(c, "Subtotal", model.Subtotal, model.Currency); if (model.DiscountAmount > 0) Total(c, "Discount", -model.DiscountAmount, model.Currency); c.Item().PaddingTop(5).BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(5).Row(r => { r.RelativeItem().Text("TOTAL").Bold().FontSize(12); r.ConstantItem(110).AlignRight().Text(Money(model.Total, model.Currency)).Bold().FontSize(12); }); });
                column.Item().PaddingTop(10).Text("This is a pro forma document and is not a fiscal invoice.").Italic().FontColor(Colors.Grey.Darken1);
            });
            page.Footer().AlignCenter().Text(x => { x.Span(model.OrganizationName); x.Span(" • Page "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
        });
    }).WithMetadata(new DocumentMetadata { Title = $"Pro forma {model.DocumentNumber}", Author = model.OrganizationName, Creator = "SimpliaDz" }).GeneratePdf();

    private static byte[] BuildReceipt(SalesDocumentModel model) => Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.ContinuousSize(226); page.Margin(12); page.DefaultTextStyle(x => x.FontSize(8));
            page.Content().Column(c =>
            {
                c.Spacing(5); c.Item().AlignCenter().Text(model.OrganizationName).Bold().FontSize(12); c.Item().AlignCenter().Text(model.LocationName); c.Item().LineHorizontal(1);
                c.Item().Text($"Receipt: {model.DocumentNumber}"); c.Item().Text(model.Date.ToString("yyyy-MM-dd HH:mm")); c.Item().Text($"Customer: {model.CustomerName}"); c.Item().LineHorizontal(1);
                foreach (var line in model.Lines) c.Item().Row(r => { r.RelativeItem().Text($"{line.ProductName}\n{line.Quantity} × {Money(line.UnitPrice, model.Currency)}"); r.ConstantItem(65).AlignRight().Text(Money(line.LineTotal, model.Currency)); });
                c.Item().LineHorizontal(1); c.Item().Row(r => { r.RelativeItem().Text("TOTAL").Bold(); r.ConstantItem(70).AlignRight().Text(Money(model.Total, model.Currency)).Bold(); });
                c.Item().AlignCenter().PaddingTop(8).Text("Thank you for your purchase");
            });
        });
    }).WithMetadata(new DocumentMetadata { Title = $"Receipt {model.DocumentNumber}", Author = model.OrganizationName, Creator = "SimpliaDz" }).GeneratePdf();

    private static IContainer Card(IContainer container) => container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10);
    private static void Header(IContainer cell, string value) => cell.Background(Colors.Blue.Darken2).Padding(6).Text(value).FontColor(Colors.White).SemiBold();
    private static void Cell(IContainer cell, string value) => cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(value);
    private static void Total(ColumnDescriptor column, string name, decimal value, string currency) => column.Item().Row(r => { r.RelativeItem().Text(name); r.ConstantItem(110).AlignRight().Text(Money(value, currency)); });
    private static string Money(decimal value, string currency) => $"{value:N2} {currency}";
    private sealed record SalesDocumentModel(string OrganizationName, string? OrganizationLegalName, string? OrganizationAddress, string? OrganizationPhone, string? OrganizationEmail, string Currency, string LocationName, string? LocationAddress, string CustomerName, string? CustomerPhone, string? CustomerAddress, string DocumentNumber, string Status, DateTimeOffset Date, decimal Subtotal, decimal DiscountAmount, decimal Total, decimal PaidAmount, IReadOnlyList<SalesDocumentLine> Lines);
    private sealed record SalesDocumentLine(string ProductName, string Sku, int Quantity, decimal UnitPrice, decimal DiscountAmount, decimal LineTotal);
}
