using System.Net;
using System.Text;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using ZXing;
using ZXing.Common;
using ZXing.OneD;

namespace SimpliaDz.Api.Modules;

public sealed record BarcodeLabelItem(string Name, string Sku, string Barcode);

public static class BarcodeLabelDocumentRenderer
{
    public static string Render(BarcodeLabelTemplate template, IReadOnlyList<BarcodeLabelItem> items)
    {
        if (items.Count == 0 || items.Count > 500)
            throw new ArgumentException("Print between 1 and 500 labels.", nameof(items));
        var columns = (int)Math.Floor((template.PageWidthMm - template.MarginMm * 2 + template.GapMm) /
            (template.LabelWidthMm + template.GapMm));
        var rows = (int)Math.Floor((template.PageHeightMm - template.MarginMm * 2 + template.GapMm) /
            (template.LabelHeightMm + template.GapMm));
        var capacity = checked(columns * rows);
        if (capacity < 1) throw new ArgumentException("No labels fit on the selected page.");
        var matrices = new Dictionary<string, BitMatrix>(StringComparer.Ordinal);
        var writer = new Code128Writer();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Barcode))
                throw new ArgumentException("Every selected variant needs a barcode.");
            if (!matrices.ContainsKey(item.Barcode))
                matrices[item.Barcode] = writer.encode(item.Barcode, BarcodeFormat.CODE_128, 0, 1);
            if ((template.LabelWidthMm - 4m) / matrices[item.Barcode].Width < 0.18m)
                throw new ArgumentException($"Barcode '{item.Barcode}' needs a wider label for reliable printing.");
        }

        var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><style>");
        html.Append(FormattableString.Invariant($"@page{{size:{template.PageWidthMm}mm {template.PageHeightMm}mm;margin:0}}"));
        html.Append("body{margin:0}svg{display:block;break-after:page}svg:last-child{break-after:auto}");
        html.Append("</style></head><body>");
        for (var offset = 0; offset < items.Count; offset += capacity)
        {
            html.Append(FormattableString.Invariant(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{template.PageWidthMm}mm\" height=\"{template.PageHeightMm}mm\" viewBox=\"0 0 {template.PageWidthMm} {template.PageHeightMm}\">"));
            var end = Math.Min(offset + capacity, items.Count);
            for (var i = offset; i < end; i++)
            {
                var position = i - offset;
                var x = template.MarginMm + position % columns * (template.LabelWidthMm + template.GapMm);
                var y = template.MarginMm + position / columns * (template.LabelHeightMm + template.GapMm);
                AppendLabel(html, template, items[i], matrices[items[i].Barcode], x, y);
            }
            html.Append("</svg>");
        }
        return html.Append("</body></html>").ToString();
    }

    private static void AppendLabel(StringBuilder html, BarcodeLabelTemplate template,
        BarcodeLabelItem item, BitMatrix matrix, decimal x, decimal y)
    {
        html.Append(FormattableString.Invariant($"<g transform=\"translate({x} {y})\">"));
        if (template.ShowName)
            AppendText(html, item.Name, 4m, template.LabelWidthMm);
        var top = template.ShowName ? 7m : 2m;
        var bottom = template.ShowSku ? 8m : 5m;
        var barHeight = template.LabelHeightMm - top - bottom;
        var moduleWidth = (template.LabelWidthMm - 4m) / matrix.Width;
        for (var i = 0; i < matrix.Width; i++)
            if (matrix[i, 0])
                html.Append(FormattableString.Invariant(
                    $"<rect x=\"{2m + i * moduleWidth:0.####}\" y=\"{top}\" width=\"{moduleWidth:0.####}\" height=\"{barHeight}\"/>"));
        if (template.ShowSku)
            AppendText(html, item.Sku, template.LabelHeightMm - 4.5m, template.LabelWidthMm);
        AppendText(html, item.Barcode, template.LabelHeightMm - 1m, template.LabelWidthMm);
        html.Append("</g>");
    }

    private static void AppendText(StringBuilder html, string value, decimal baseline,
        decimal labelWidth)
    {
        var escaped = WebUtility.HtmlEncode(value);
        html.Append(FormattableString.Invariant(
            $"<text x=\"2\" y=\"{baseline}\" font-family=\"Arial,sans-serif\" font-size=\"2.5\""));
        if (value.Length > 30)
            html.Append(FormattableString.Invariant(
                $" textLength=\"{labelWidth - 4m}\" lengthAdjust=\"spacingAndGlyphs\""));
        html.Append('>');
        html.Append(escaped).Append("</text>");
    }
}
