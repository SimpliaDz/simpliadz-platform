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
            page.Size(PageSizes.A4); page.Margin(28); page.DefaultTextStyle(x => x.FontSize(8));
            page.Content().Column(column =>
            {
                column.Spacing(10);
                column.Item().Row(row =>
                {
                    row.RelativeItem(3).Column(c =>
                    {
                        c.Item().Text(model.OrganizationName).Bold().FontSize(11);
                        if (model.OrganizationAddress is not null) c.Item().Text(model.OrganizationAddress);
                        if (model.OrganizationPhone is not null) c.Item().Text($"Téléphone : {model.OrganizationPhone}");
                        if (model.OrganizationEmail is not null) c.Item().Text($"Email : {model.OrganizationEmail}");
                    });
                    row.RelativeItem(2).AlignRight().Column(c =>
                    {
                        c.Item().Text("Destinataire").Bold(); c.Item().Text(model.CustomerName).Bold();
                        if (model.CustomerAddress is not null) c.Item().Text(model.CustomerAddress);
                        if (model.CustomerPhone is not null) c.Item().Text(model.CustomerPhone);
                    });
                });
                column.Item().Row(row =>
                {
                    row.RelativeItem(3).Background(Colors.Grey.Lighten3).Padding(8).AlignRight().Column(c =>
                    {
                        c.Item().Text($"Date d'émission   {model.Date:dd/MM/yyyy}");
                        c.Item().Text($"Émis par   {model.LocationName}");
                        c.Item().PaddingTop(5).Text("Délai de livraison   À réception du paiement");
                        c.Item().Text("Mode de livraison   DHL");
                        c.Item().Text("Modalité de paiement   30 jours");
                    });
                    row.RelativeItem(2);
                });
                column.Item().AlignRight().Text($"{title} n°{model.DocumentNumber}").Bold().FontSize(14);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(4); c.ConstantColumn(52); c.ConstantColumn(48); c.ConstantColumn(78); c.ConstantColumn(48); c.ConstantColumn(90); });
                    table.Header(h =>
                    {
                        FrenchHeader(h.Cell(), "Désignation des produits\nou prestations"); FrenchHeader(h.Cell(), "Quantité");
                        FrenchHeader(h.Cell(), "Unité"); FrenchHeader(h.Cell(), "Prix unitaire HT");
                        FrenchHeader(h.Cell(), "TVA\napplicable"); FrenchHeader(h.Cell(), "TOTAL HT");
                    });
                    foreach (var line in model.Lines)
                    {
                        FrenchCell(table.Cell(), $"{line.ProductName}\n{line.Sku}"); FrenchCell(table.Cell().AlignRight(), line.Quantity.ToString());
                        FrenchCell(table.Cell().AlignCenter(), "u."); FrenchCell(table.Cell().AlignRight(), Money(line.UnitPrice, model.Currency));
                        FrenchCell(table.Cell().AlignCenter(), "—"); FrenchCell(table.Cell().AlignRight(), Money(line.LineTotal, model.Currency));
                    }
                    if (model.Lines.Count < 4)
                    {
                        var remaining = 4 - model.Lines.Count;
                        FrenchCell(table.Cell().MinHeight(remaining * 28), ""); FrenchCell(table.Cell(), ""); FrenchCell(table.Cell(), ""); FrenchCell(table.Cell(), ""); FrenchCell(table.Cell(), ""); FrenchCell(table.Cell(), "");
                    }
                });
                column.Item().Row(row =>
                {
                    row.RelativeItem().PaddingTop(12).Column(c => { c.Item().Text("Offre valable jusqu'au " + model.Date.AddDays(30).ToString("dd/MM/yyyy")); c.Item().PaddingTop(18).Text("Signature").Bold(); });
                    row.ConstantItem(235).Column(c =>
                    {
                        FrenchTotal(c, "Total HT", model.Subtotal, model.Currency);
                        FrenchTotalText(c, "TVA", "—");
                        if (model.DiscountAmount > 0) FrenchTotal(c, "Remise", -model.DiscountAmount, model.Currency);
                        FrenchTotal(c, "Frais de port", 0, model.Currency);
                        c.Item().BorderTop(1).BorderColor(Colors.Grey.Darken2).PaddingTop(5).Row(r => { r.RelativeItem().Text("Total TTC").Bold().FontSize(10); r.ConstantItem(105).AlignRight().Text(Money(model.Total, model.Currency)).Bold().FontSize(10); });
                    });
                });
                column.Item().PaddingTop(7).Text("Document pro forma — non valable comme facture fiscale.").Italic().FontColor(Colors.Grey.Darken1);
            });
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
    private static void FrenchHeader(IContainer cell, string value) => cell.Border(1).BorderColor(Colors.Grey.Darken2).Background("D9E8F6").Padding(5).AlignCenter().Text(value).Bold().FontSize(7);
    private static void FrenchCell(IContainer cell, string value) => cell.Border(1).BorderColor(Colors.Grey.Darken2).Padding(5).Text(value);
    private static void FrenchTotal(ColumnDescriptor column, string name, decimal value, string currency) => column.Item().Row(r => { r.RelativeItem().Text(name).Bold(); r.ConstantItem(105).AlignRight().Text(Money(value, currency)); });
    private static void FrenchTotalText(ColumnDescriptor column, string name, string value) => column.Item().Row(r => { r.RelativeItem().Text(name).Bold(); r.ConstantItem(105).AlignRight().Text(value); });
    private static void Total(ColumnDescriptor column, string name, decimal value, string currency) => column.Item().Row(r => { r.RelativeItem().Text(name); r.ConstantItem(110).AlignRight().Text(Money(value, currency)); });
    private static string Money(decimal value, string currency) => $"{value:N2} {currency}";
    private sealed record SalesDocumentModel(string OrganizationName, string? OrganizationLegalName, string? OrganizationAddress, string? OrganizationPhone, string? OrganizationEmail, string Currency, string LocationName, string? LocationAddress, string CustomerName, string? CustomerPhone, string? CustomerAddress, string DocumentNumber, string Status, DateTimeOffset Date, decimal Subtotal, decimal DiscountAmount, decimal Total, decimal PaidAmount, IReadOnlyList<SalesDocumentLine> Lines);
    private sealed record SalesDocumentLine(string ProductName, string Sku, int Quantity, decimal UnitPrice, decimal DiscountAmount, decimal LineTotal);
}
