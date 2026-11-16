using System.Net;
using System.Text;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Api.Modules;

public static class InventoryDocumentRenderer
{
    public static string Render(InventoryDocumentTemplate template, string context,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><style>");
        html.Append($"@page{{size:{template.PaperSize} {(template.Landscape ? "landscape" : "portrait")};margin:12mm}} ");
        html.Append("body{font:11pt Arial,sans-serif;color:#111}h1{font-size:16pt;margin:0 0 3mm}");
        html.Append("p.meta{font-size:9pt;margin:0 0 7mm}table{border-collapse:collapse;width:100%}");
        html.Append("th,td{border:1px solid #666;padding:2mm;text-align:left}th{background:#eee}");
        html.Append("thead{display:table-header-group}tr{break-inside:avoid}footer{margin-top:7mm;font-size:9pt}");
        html.Append("</style></head><body><h1>").Append(WebUtility.HtmlEncode(template.Title))
            .Append("</h1><p class=\"meta\">").Append(WebUtility.HtmlEncode(context))
            .Append(" · Generated ").Append(DateTimeOffset.UtcNow.ToString("u"))
            .Append("</p><table><thead><tr>");
        var columns = template.GetColumns();
        foreach (var column in columns)
            html.Append("<th>").Append(WebUtility.HtmlEncode(column)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            html.Append("<tr>");
            foreach (var column in columns)
            {
                row.TryGetValue(column, out var value);
                html.Append("<td>").Append(WebUtility.HtmlEncode(value ?? ""))
                    .Append("</td>");
            }
            html.Append("</tr>");
        }
        html.Append("</tbody></table>");
        if (template.FooterText is not null)
            html.Append("<footer>").Append(WebUtility.HtmlEncode(template.FooterText))
                .Append("</footer>");
        return html.Append("</body></html>").ToString();
    }
}
