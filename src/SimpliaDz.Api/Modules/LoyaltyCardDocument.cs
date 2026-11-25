using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Organization.Infrastructure.Data;
using ZXing;
using ZXing.OneD;

namespace SimpliaDz.Api.Modules;

public static class LoyaltyCardDocument
{
    public static Task<string?> OrganizationNameAsync(OrganizationDbContext db, CancellationToken ct) =>
        db.Organizations.AsNoTracking().Select(x => x.Name).FirstOrDefaultAsync(ct);

    public static string Artwork(string customerName, Guid customerId, string cardNumber, string? organizationName)
    {
        var matrix = new Code128Writer().encode(customerId.ToString("N"),
            BarcodeFormat.CODE_128, 0, 1);
        var bars = new StringBuilder();
        for (var x = 0; x < matrix.Width; x++)
            if (matrix[x, 0]) bars.Append($"<rect x='{x + 10}' width='1' height='60'/>");
        var brand = Label(organizationName ?? "Simplia", 38);
        var name = Label(customerName, 42);
        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="680" height="440" viewBox="0 0 680 440">
              <rect width="680" height="440" rx="28" fill="#203c34"/>
              <path d="M0 115Q115 140 80 0M0 146Q155 160 110 0M680 300Q540 290 578 440M680 329Q574 319 609 440" fill="none" stroke="#8fb6a0" stroke-width="3"/>
              <circle cx="340" cy="65" r="24" fill="none" stroke="#dfc88f" stroke-width="2"/>
              <path d="M328 68l9 8 15-20" fill="none" stroke="#dfc88f" stroke-width="3" stroke-linecap="round"/>
              <g text-anchor="middle" font-family="Lato, Arial, sans-serif">
                <text x="340" y="128" fill="#fff8e8" font-size="25" font-weight="bold">{{brand}}</text>
                <text x="340" y="158" fill="#dfc88f" font-size="15" letter-spacing="3">CARTE FIDÉLITÉ</text>
                <text x="340" y="200" fill="#fff8e8" font-size="20">{{name}}</text>
                <rect x="72" y="228" width="536" height="139" rx="12" fill="white"/>
                <svg x="92" y="249" width="496" height="72" viewBox="0 0 {{matrix.Width + 20}} 60" preserveAspectRatio="none">{{bars}}</svg>
                <text x="340" y="346" fill="#203c34" font-size="15">{{WebUtility.HtmlEncode(cardNumber)}}</text>
                <text x="340" y="407" fill="#dce8de" font-size="15">À présenter en caisse</text>
              </g>
            </svg>
            """;
    }

    private static string Label(string value, int limit) =>
        WebUtility.HtmlEncode(value.Length <= limit ? value : value[..limit] + "…");

    public static byte[] Render(string customerName, Guid customerId, string cardNumber,
        string? organizationName = null) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(340, 220);
            page.Content().Svg(Artwork(customerName, customerId, cardNumber, organizationName)).FitArea();
        })).GeneratePdf();

    public static async Task SendAsync(OrganizationDbContext db, IDataProtectionProvider protection,
        string recipient, byte[] pdf, CancellationToken ct)
    {
        var settings = await db.OrganizationSettings.AsNoTracking()
            .Where(x => x.Key.StartsWith("Notifications.")).ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        string? Get(string key) => settings.GetValueOrDefault($"Notifications.{key}");
        if (!string.Equals(Get("Email.Enabled"), "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Get("Smtp.Host")) ||
            !MailAddress.TryCreate(Get("Email.FromAddress"), out var sender))
            throw new ConflictException("Enable email and configure the sender and SMTP host in Organization Settings.");
        if (!MailAddress.TryCreate(recipient, out var address))
            throw new ConflictException("Add a valid email address to the customer first.");

        using var client = new SmtpClient(Get("Smtp.Host")!, int.TryParse(Get("Smtp.Port"), out var port) ? port : 587)
        {
            EnableSsl = !string.Equals(Get("Smtp.UseStartTls"), "false", StringComparison.OrdinalIgnoreCase)
        };
        if (Get("Smtp.Username") is { Length: > 0 } username)
        {
            if (!client.EnableSsl || Get("Smtp.Password") is not { Length: > 0 } encrypted)
                throw new ConflictException("Authenticated SMTP requires STARTTLS and a configured password.");
            client.Credentials = new NetworkCredential(username,
                protection.CreateProtector("SimpliaDz.OrganizationSettings.v1").Unprotect(encrypted));
        }
        using var message = new MailMessage(new MailAddress(sender.Address, Get("Email.FromName") ?? "Simplia"), address)
        {
            Subject = "Votre carte de fidélité",
            Body = "Votre carte de fidélité est jointe à ce message. Présentez son code-barres en caisse."
        };
        message.Attachments.Add(new Attachment(new MemoryStream(pdf), "carte-fidelite.pdf", "application/pdf"));
        try { await client.SendMailAsync(message, ct); }
        catch (SmtpException) { throw new ConflictException("Email could not be sent. Check the organization's SMTP settings."); }
    }
}
