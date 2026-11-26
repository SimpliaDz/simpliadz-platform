using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Organization.Infrastructure.Data;
using Simplia.Sales.Core.Entities;
using Simplia.Sales.Infrastructure.Data;
using ZXing;
using ZXing.OneD;

namespace Simplia.Api.Modules;

public sealed record GeneratedPdf(string FileName, byte[] Content);

/// <summary>Generates sales documents from recorded sales snapshots, never current catalogue prices.</summary>
public sealed class SalesPdfDocumentService(SalesDbContext sales, InventoryDbContext inventory,
    OrganizationDbContext organization)
{
    public async Task<GeneratedPdf> ProFormaAsync(Guid saleId, CancellationToken ct)
    {
        var model = await LoadAsync(saleId, ct);
        model = model with { DocumentNumber = await GetOrAssignDocumentNumberAsync(saleId, "ProForma", ct) };
        return new($"pro-forma-{model.DocumentNumber}.pdf", BuildA4(model, "PRO FORMA INVOICE"));
    }

    public async Task<GeneratedPdf> ReceiptAsync(Guid saleId, CancellationToken ct)
    {
        var model = await LoadAsync(saleId, ct);
        if (model.Status is not ("Completed" or "PartiallyReturned" or "Returned"))
            throw new ConflictException("A receipt can only be generated for a completed sale.");
        var methods = await sales.Payments.AsNoTracking().Where(x => x.SaleId == saleId)
            .GroupBy(x => x.PaymentMethod).Select(g => new ReceiptPayment(g.Key, g.Sum(x => x.Amount)))
            .ToListAsync(ct);
        return new($"receipt-{model.DocumentNumber}.pdf", BuildReceipt(model, saleId, methods));
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
            profile?.Name ?? "Simplia", profile?.LegalName, profile?.Address, profile?.Phone,
            profile?.Email, profile?.CurrencyCode ?? "DZD", location?.Name ?? "Location", location?.Address,
            sale.Customer?.Name ?? "Walk-in customer", sale.Customer?.Phone, sale.Customer?.Address,
            sale.SaleNumber ?? $"PF-{sale.Id.ToString("N")[..10].ToUpperInvariant()}", sale.Status.ToString(),
            sale.SaleDate, sale.Subtotal, sale.DiscountAmount, sale.Total, sale.PaidAmount,
            sale.Lines.Select(line => new SalesDocumentLine(
                variants.GetValueOrDefault(line.ProductVariantId)?.ProductName ?? "Product",
                variants.GetValueOrDefault(line.ProductVariantId)?.Sku ?? line.ProductVariantId.ToString(),
                line.Quantity, line.UnitPrice, line.DiscountAmount, line.LineTotal)).ToList());
    }

    private async Task<string> GetOrAssignDocumentNumberAsync(Guid saleId, string documentType,
        CancellationToken ct)
    {
        var issuedYear = DateTime.UtcNow.Year;
        await using var transaction = await sales.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var existing = await sales.SalesDocumentNumbers.SingleOrDefaultAsync(x =>
                x.SaleId == saleId && x.DocumentType == documentType, ct);
            if (existing is not null)
            {
                await transaction.CommitAsync(ct);
                return existing.DisplayNumber;
            }
            var last = await sales.SalesDocumentNumbers.Where(x =>
                    x.DocumentType == documentType && x.IssueYear == issuedYear)
                .Select(x => (int?)x.SequenceNumber).MaxAsync(ct) ?? 0;
            var number = new SalesDocumentNumber(saleId, documentType, issuedYear,
                checked(last + 1));
            sales.SalesDocumentNumbers.Add(number);
            await sales.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return number.DisplayNumber;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
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
    }).WithMetadata(new DocumentMetadata { Title = $"Pro forma {model.DocumentNumber}", Author = model.OrganizationName, Creator = "Simplia" }).GeneratePdf();

    private static byte[] BuildReceipt(SalesDocumentModel model, Guid saleId, IReadOnlyList<ReceiptPayment> payments) => Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.ContinuousSize(80, Unit.Millimetre);
            page.Margin(4, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Content().Column(c =>
            {
                c.Spacing(5);
                c.Item().AlignCenter().Text(model.OrganizationName).Bold().FontSize(15);
                c.Item().AlignCenter().Text(model.LocationName);
                var address = model.LocationAddress ?? model.OrganizationAddress;
                if (!string.IsNullOrWhiteSpace(address)) c.Item().AlignCenter().Text(address);
                if (!string.IsNullOrWhiteSpace(model.OrganizationPhone)) c.Item().AlignCenter().Text(model.OrganizationPhone);
                c.Item().PaddingVertical(3).LineHorizontal(1);
                c.Item().AlignCenter().Text("TICKET DE CAISSE").Bold().FontSize(12);
                c.Item().Text($"Ticket : {model.DocumentNumber}");
                c.Item().Text($"Date : {model.Date:dd/MM/yyyy HH:mm zzz}");
                c.Item().Text($"Client : {model.CustomerName}");
                c.Item().PaddingVertical(3).LineHorizontal(1);
                foreach (var line in model.Lines)
                {
                    c.Item().Text(line.ProductName).SemiBold();
                    ReceiptAmount(c, $"{line.Quantity} × {Money(line.UnitPrice, model.Currency)}",
                        line.LineTotal, model.Currency);
                }
                c.Item().PaddingVertical(3).LineHorizontal(1);
                if (model.DiscountAmount > 0)
                    ReceiptAmount(c, "Remise", -model.DiscountAmount, model.Currency);
                ReceiptAmount(c, "TOTAL", model.Total, model.Currency, emphasized: true);
                ReceiptAmount(c, "Montant payé", model.PaidAmount, model.Currency);
                foreach (var payment in payments)
                {
                    var method = payment.Method switch
                    {
                        PaymentMethod.Cash => "Espèces",
                        PaymentMethod.Card => "Carte",
                        PaymentMethod.BankTransfer => "Virement",
                        _ => "Autre"
                    };
                    ReceiptAmount(c, method, payment.Amount, model.Currency);
                }
                if (model.PaidAmount < model.Total)
                {
                    ReceiptAmount(c, "Solde restant", model.Total - model.PaidAmount, model.Currency, emphasized: true);
                    c.Item().Text(model.PaidAmount == 0 ? "À crédit" : "Partiellement payé").SemiBold();
                    if (!string.IsNullOrWhiteSpace(model.CustomerPhone))
                        c.Item().Text(model.CustomerPhone);
                }
                c.Item().PaddingTop(8).AlignCenter().Text("Merci pour votre achat !").SemiBold();
                c.Item().PaddingTop(5).Height(30).Svg(ReceiptBarcode(saleId)).FitArea();
                c.Item().AlignCenter().Text(saleId.ToString("N")).FontSize(7);
            });
        });
    }).WithMetadata(new DocumentMetadata { Title = $"Receipt {model.DocumentNumber}", Author = model.OrganizationName, Creator = "Simplia" }).GeneratePdf();

    private static void ReceiptAmount(ColumnDescriptor column, string label, decimal amount, string currency,
        bool emphasized = false) => column.Item().Row(row =>
    {
        var title = row.RelativeItem().Text(label);
        var value = row.AutoItem().PaddingLeft(6).Text(Money(amount, currency));
        if (emphasized) { title.Bold().FontSize(12); value.Bold().FontSize(12); }
    });

    private static string ReceiptBarcode(Guid saleId)
    {
        var matrix = new Code128Writer().encode(saleId.ToString("N"), BarcodeFormat.CODE_128, 0, 1);
        var width = matrix.Width + 20;
        var bars = new StringBuilder();
        for (var x = 0; x < matrix.Width; x++)
            if (matrix[x, 0]) bars.Append(FormattableString.Invariant($"<rect x='{x + 10}' width='1' height='60'/>"));
        return FormattableString.Invariant($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} 60' preserveAspectRatio='none'>{bars}</svg>");
    }

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
    private sealed record ReceiptPayment(PaymentMethod Method, decimal Amount);
    private sealed record SalesDocumentLine(string ProductName, string Sku, int Quantity, decimal UnitPrice, decimal DiscountAmount, decimal LineTotal);
}
