using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class BarcodeLabelEndpoints
{
    public static IEndpointRouteBuilder MapBarcodeLabelEndpoints(this IEndpointRouteBuilder app)
    {
        var templates = app.MapGroup("/barcode-label-templates")
            .WithTags("Barcode Labels")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        templates.MapGet("/", async ([FromServices] InventoryDbContext db, CancellationToken ct) =>
            Results.Ok(await db.BarcodeLabelTemplates.AsNoTracking()
                .OrderBy(x => x.Name).ToListAsync(ct)))
            .RequireAuthorization(PermissionNames.BarcodeLabels.View);

        templates.MapPost("/", async ([FromBody] BarcodeLabelTemplateRequest request,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var template = NewTemplate(request);
            db.BarcodeLabelTemplates.Add(template);
            await SaveAsync(db, ct);
            return Results.Created($"/barcode-label-templates/{template.Id}", template);
        }).RequireAuthorization(PermissionNames.BarcodeLabels.Manage);

        templates.MapPut("/{id:guid}", async (Guid id,
            [FromBody] BarcodeLabelTemplateRequest request,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var template = await db.BarcodeLabelTemplates.FindAsync([id], ct)
                ?? throw new NotFoundException("Barcode label template", id);
            if (!template.IsActive) throw new ConflictException("Template is inactive.");
            template.Update(request.Name, request.PageWidthMm, request.PageHeightMm,
                request.LabelWidthMm, request.LabelHeightMm, request.MarginMm,
                request.GapMm, request.ShowName, request.ShowSku);
            await SaveAsync(db, ct);
            return Results.Ok(template);
        }).RequireAuthorization(PermissionNames.BarcodeLabels.Manage);

        templates.MapPut("/{id:guid}/activate", async (Guid id, HttpContext http,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var template = await db.BarcodeLabelTemplates.FindAsync([id], ct)
                ?? throw new NotFoundException("Barcode label template", id);
            if (template.IsActive) return Results.NoContent();
            template.Activate();
            await SaveAsync(db, ct);
            await BusinessAudit.RecordIfEnabledAsync(http, "TemplateActivated", "BarcodeLabelTemplate", id,
                new Dictionary<string, object?> { ["name"] = template.Name }, ct);
            return Results.NoContent();
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization(PermissionNames.BarcodeLabels.Manage);

        templates.MapPut("/{id:guid}/deactivate", async (Guid id,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var template = await db.BarcodeLabelTemplates.FindAsync([id], ct)
                ?? throw new NotFoundException("Barcode label template", id);
            template.Deactivate();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.BarcodeLabels.Manage);

        app.MapPost("/barcode-labels/print-document", async (
            [FromBody] BarcodeLabelPrintRequest request, HttpContext http,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            if (request.TemplateId == Guid.Empty || request.Lines is null ||
                request.Lines.Count == 0 || request.Lines.Any(x => x.VariantId == Guid.Empty ||
                    x.Quantity <= 0) || request.Lines.Sum(x => (long)x.Quantity) > 500)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["lines"] = ["Select a template and between 1 and 500 labels with valid variants and positive quantities."] });
            var template = await db.BarcodeLabelTemplates.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.TemplateId, ct)
                ?? throw new NotFoundException("Barcode label template", request.TemplateId);
            if (!template.IsActive) throw new ConflictException("Template is inactive.");
            var ids = request.Lines.Select(x => x.VariantId).Distinct().ToArray();
            var variants = await db.ProductVariants.AsNoTracking().Include(x => x.Product)
                .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            if (variants.Count != ids.Length || variants.Values.Any(x => !x.IsActive ||
                !x.Product.IsActive || x.Barcode is null))
                throw new ConflictException("Every selected variant must be active and have a barcode.");
            var items = request.Lines.SelectMany(x => Enumerable.Repeat(
                new BarcodeLabelItem(variants[x.VariantId].Product.Name.Value,
                    variants[x.VariantId].Sku.Value, variants[x.VariantId].Barcode!),
                x.Quantity)).ToArray();
            var html = BarcodeLabelDocumentRenderer.Render(template, items);
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
            http.Response.Headers.ContentDisposition = "inline; filename=barcode-labels.html";
            return Results.Content(html, "text/html; charset=utf-8");
        }).WithTags("Barcode Labels")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"))
            .RequireAuthorization(PermissionNames.BarcodeLabels.Print);

        return app;
    }

    private static BarcodeLabelTemplate NewTemplate(BarcodeLabelTemplateRequest request) =>
        new(request.Name, request.PageWidthMm, request.PageHeightMm,
            request.LabelWidthMm, request.LabelHeightMm, request.MarginMm,
            request.GapMm, request.ShowName, request.ShowSku);

    private static async Task SaveAsync(InventoryDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("A barcode label template with this name already exists.");
        }
    }
}

public sealed record BarcodeLabelTemplateRequest(string Name, decimal PageWidthMm,
    decimal PageHeightMm, decimal LabelWidthMm, decimal LabelHeightMm,
    decimal MarginMm, decimal GapMm, bool ShowName, bool ShowSku);
public sealed record BarcodeLabelPrintLine(Guid VariantId, int Quantity);
public sealed record BarcodeLabelPrintRequest(Guid TemplateId,
    IReadOnlyList<BarcodeLabelPrintLine> Lines);
