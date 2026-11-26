using Mediator;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simplia.Api.Modules;
using Simplia.Audit.UseCases;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Organization.Contracts;
using Simplia.Organization.UseCases.Commands.UpdateOrganization;
using Simplia.Organization.UseCases.Queries.GetOrganization;
using Simplia.Organization.Core.Entities;
using Simplia.Organization.Infrastructure.Data;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Organization;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/organization")
            .WithTags("Organization")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Organization"));

        group.MapGet("/", async ([FromServices] ISender sender, CancellationToken ct) =>
        {
            var organization = await sender.Send(new GetOrganizationQuery(), ct);
            return Results.Ok(organization);
        }).RequireAuthorization(PermissionNames.Organization.View);

        group.MapPut("/", async (
            [FromBody] UpdateOrganizationRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var organization = await sender.Send(
                new UpdateOrganizationCommand(
                    request.Name,
                    request.LegalName,
                    request.Phone,
                    request.Email,
                    request.Address,
                    request.CurrencyCode,
                    request.DefaultLanguage,
                    request.IsActive,
                    request.TimeZoneId),
                ct);

            return Results.Ok(organization);
        }).RequireAuthorization(PermissionNames.Organization.Manage);

        group.MapGet("/settings", async ([FromServices] OrganizationDbContext db,
            CancellationToken ct) => Results.Ok(await db.OrganizationSettings.AsNoTracking()
                .OrderBy(x => x.Key).Select(x => new OrganizationSettingResponse(x.Key,
                    x.IsSecret ? null : x.Value, x.ValueType, x.IsSecret, x.IsSecret,
                    x.UpdatedAt, x.UpdatedBy)).ToListAsync(ct)))
            .RequireAuthorization(PermissionNames.Organization.View);

        group.MapPut("/settings/{key}", async (string key, [FromBody] UpdateOrganizationSettingRequest request,
            HttpContext http, [FromServices] OrganizationDbContext db,
            [FromServices] IDataProtectionProvider protection, [FromServices] IAuditTrail audit,
            CancellationToken ct) =>
        {
            if (!OrganizationSettingKeys.Definitions.TryGetValue(key, out var definition))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["key"] = ["This setting is not supported."] });
            if (request.Value is null || !definition.IsValid(request.Value))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["value"] = [definition.ValidationMessage] });
            var actor = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var setting = await db.OrganizationSettings.SingleOrDefaultAsync(x => x.Key == key, ct);
            var stored = definition.IsSecret
                ? protection.CreateProtector("Simplia.OrganizationSettings.v1").Protect(request.Value)
                : request.Value;
            var old = setting is null ? null : new Dictionary<string, object?> { ["configured"] = true };
            if (setting is null) { setting = new OrganizationSetting(key, stored, definition.ValueType, definition.IsSecret, actor); db.OrganizationSettings.Add(setting); }
            else setting.Update(key, stored, definition.ValueType, definition.IsSecret, actor);
            await db.SaveChangesAsync(ct);
            await BusinessAudit.RecordAsync(audit, http, "OrganizationSettingUpdated", "OrganizationSetting", setting.Id,
                old, new Dictionary<string, object?> { ["key"] = key, ["isSecret"] = definition.IsSecret }, ct);
            return Results.Ok(new OrganizationSettingResponse(key, definition.IsSecret ? null : request.Value,
                definition.ValueType, definition.IsSecret, definition.IsSecret, setting.UpdatedAt, actor));
        }).RequireAudit().RequireAuthorization(PermissionNames.Organization.Manage);

        return app;
    }
}

public sealed record OrganizationSettingResponse(string Key, string? Value, string ValueType,
    bool IsSecret, bool HasValue, DateTimeOffset UpdatedAt, Guid UpdatedBy);
public sealed record UpdateOrganizationSettingRequest(string? Value);
public sealed record OrganizationSettingDefinition(string ValueType, bool IsSecret,
    Func<string, bool> IsValid, string ValidationMessage);
public static class OrganizationSettingKeys
{
    public static readonly IReadOnlyDictionary<string, OrganizationSettingDefinition> Definitions =
        new Dictionary<string, OrganizationSettingDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Notifications.Email.Enabled"] = new("Boolean", false, x => bool.TryParse(x, out _), "Enter true or false."),
            ["Notifications.Email.FromAddress"] = new("String", false, x => x.Length <= 254 && x.Contains('@'), "Enter a valid sender email."),
            ["Notifications.Email.FromName"] = new("String", false, x => x.Length is > 0 and <= 120, "Enter a sender name of at most 120 characters."),
            ["Notifications.Smtp.Host"] = new("String", false, x => x.Length is > 0 and <= 255, "Enter an SMTP host."),
            ["Notifications.Smtp.Port"] = new("Integer", false, x => int.TryParse(x, out var p) && p is > 0 and <= 65535, "Enter a valid SMTP port."),
            ["Notifications.Smtp.Username"] = new("String", false, x => x.Length <= 254, "Username must fit 254 characters."),
            ["Notifications.Smtp.Password"] = new("String", true, x => x.Length is > 0 and <= 4000, "Password is required."),
            ["Notifications.Smtp.UseStartTls"] = new("Boolean", false, x => bool.TryParse(x, out _), "Enter true or false."),
            ["Documents.Receipt.Footer"] = new("String", false, x => x.Length <= 500, "Footer must fit 500 characters."),
            ["Documents.Invoice.Footer"] = new("String", false, x => x.Length <= 500, "Footer must fit 500 characters."),
            ["Documents.ShowLogo"] = new("Boolean", false, x => bool.TryParse(x, out _), "Enter true or false."),
            ["General.FirstDayOfWeek"] = new("String", false, x => x is "Monday" or "Sunday", "Choose Monday or Sunday.")
        };
}
