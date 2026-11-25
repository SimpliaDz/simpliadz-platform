using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class InventoryDocumentEndpoints
{
    public static IEndpointRouteBuilder MapInventoryDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var templates = app.MapGroup("/inventory-document-templates")
            .WithTags("Inventory Documents")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        templates.MapGet("/", async ([FromServices] InventoryDbContext db, CancellationToken ct) =>
            Results.Ok(await db.InventoryDocumentTemplates.AsNoTracking()
                .OrderBy(x => x.Type).ThenBy(x => x.Name).ToListAsync(ct)))
            .RequireAuthorization(PermissionNames.InventoryDocuments.View);

        templates.MapPost("/", async ([FromBody] CreateInventoryDocumentTemplateRequest request,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            if (!Enum.TryParse<InventoryDocumentType>(request.Type, true, out var type) ||
                !Enum.IsDefined(type))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["type"] = ["Type must be StockList, CountSheet, or CountDifferences."] });
            var template = new InventoryDocumentTemplate(request.Name, type,
                request.Title, request.Columns, request.PaperSize,
                request.Landscape, request.FooterText);
            db.InventoryDocumentTemplates.Add(template);
            await SaveAsync(db, ct);
            return Results.Created($"/inventory-document-templates/{template.Id}", template);
        }).RequireAuthorization(PermissionNames.InventoryDocuments.Manage);

        templates.MapPut("/{id:guid}", async (Guid id,
            [FromBody] UpdateInventoryDocumentTemplateRequest request,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var template = await db.InventoryDocumentTemplates.FindAsync([id], ct)
                ?? throw new NotFoundException("Inventory document template", id);
            if (!template.IsActive) throw new ConflictException("Template is inactive.");
            template.Update(request.Name, request.Title, request.Columns,
                request.PaperSize, request.Landscape, request.FooterText);
            await SaveAsync(db, ct);
            return Results.Ok(template);
        }).RequireAuthorization(PermissionNames.InventoryDocuments.Manage);

        templates.MapPut("/{id:guid}/activate", async (Guid id, HttpContext http,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var template = await db.InventoryDocumentTemplates.FindAsync([id], ct)
                ?? throw new NotFoundException("Inventory document template", id);
            if (template.IsActive) return Results.NoContent();
            template.Activate();
            await SaveAsync(db, ct);
            await BusinessAudit.RecordIfEnabledAsync(http, "TemplateActivated", "InventoryDocumentTemplate", id,
                new Dictionary<string, object?> { ["name"] = template.Name }, ct);
            return Results.NoContent();
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization(PermissionNames.InventoryDocuments.Manage);

        templates.MapPut("/{id:guid}/deactivate", async (Guid id,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var template = await db.InventoryDocumentTemplates.FindAsync([id], ct)
                ?? throw new NotFoundException("Inventory document template", id);
            template.Deactivate();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.InventoryDocuments.Manage);

        app.MapGet("/reports/inventory/document", async (Guid templateId, Guid? locationId,
            bool? includeZeroStock, HttpContext http, [FromServices] InventoryDbContext db,
            CancellationToken ct) =>
        {
            var template = await LoadTemplate(db, templateId, InventoryDocumentType.StockList, ct);
            if (locationId == Guid.Empty) throw new ArgumentException("Location ID must be valid.");
            if (locationId.HasValue && !await db.Locations.AnyAsync(x => x.Id == locationId.Value, ct))
                throw new NotFoundException("Location", locationId.Value);
            var variants = await db.ProductVariants.AsNoTracking().Include(x => x.Product)
                .OrderBy(x => x.Sku).Take(5001).ToListAsync(ct);
            if (variants.Count > 5000)
                throw new ConflictException("Stock document exceeds 5000 variants; filter or split the document.");
            var balances = await db.StockBalances.AsNoTracking()
                .Where(x => !locationId.HasValue || x.LocationId == locationId.Value)
                .GroupBy(x => x.VariantId)
                .Select(x => new { VariantId = x.Key, Quantity = x.Sum(y => (long)y.QuantityOnHand) })
                .ToDictionaryAsync(x => x.VariantId, x => x.Quantity, ct);
            var rows = variants.Select(x => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                {
                    ["Product"] = x.Product.Name.Value, ["Sku"] = x.Sku.Value,
                    ["Barcode"] = x.Barcode ?? "", ["Quantity"] =
                        balances.GetValueOrDefault(x.Id).ToString()
                }).Where(x => includeZeroStock == true || x["Quantity"] != "0").ToArray();
            var context = locationId.HasValue ? $"Location: {locationId}" : "All locations";
            return Print(http, InventoryDocumentRenderer.Render(template, context, rows),
                "inventory-stock-list.html");
        }).WithTags("Inventory Documents")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"))
            .RequireAuthorization(PermissionNames.InventoryDocuments.Print);

        app.MapGet("/inventory-counts/{id:guid}/count-sheet", async (Guid id, Guid templateId,
            HttpContext http, [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var template = await LoadTemplate(db, templateId, InventoryDocumentType.CountSheet, ct);
            var count = await LoadCount(db, id, ct);
            var rows = count.Lines.OrderBy(x => x.ProductVariant.Sku.Value)
                .Select(x => (IReadOnlyDictionary<string, string>)CountRow(x, false)).ToArray();
            return Print(http, InventoryDocumentRenderer.Render(template,
                $"Location: {count.Location.Name} · Count: {count.Id}", rows), "count-sheet.html");
        }).WithTags("Inventory Documents")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"))
            .RequireAuthorization(PermissionNames.InventoryDocuments.Print);

        app.MapGet("/inventory-counts/{id:guid}/difference-document", async (Guid id,
            Guid templateId, HttpContext http, [FromServices] InventoryDbContext db,
            CancellationToken ct) =>
        {
            var template = await LoadTemplate(db, templateId,
                InventoryDocumentType.CountDifferences, ct);
            var count = await LoadCount(db, id, ct);
            if (count.Status != InventoryCountStatus.Completed)
                throw new ConflictException("Complete the count before printing differences.");
            var rows = count.Lines.Where(x => x.Difference != 0)
                .OrderBy(x => x.ProductVariant.Sku.Value)
                .Select(x => (IReadOnlyDictionary<string, string>)CountRow(x, true)).ToArray();
            return Print(http, InventoryDocumentRenderer.Render(template,
                $"Location: {count.Location.Name} · Count: {count.Id}", rows),
                "count-differences.html");
        }).WithTags("Inventory Documents")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"))
            .RequireAuthorization(PermissionNames.InventoryDocuments.Print);

        return app;
    }

    private static Dictionary<string, string> CountRow(InventoryCountLine line, bool includeDifference) =>
        new()
        {
            ["Product"] = line.ProductVariant.Product.Name.Value,
            ["Sku"] = line.ProductVariant.Sku.Value,
            ["Barcode"] = line.ProductVariant.Barcode ?? "",
            ["Expected"] = line.ExpectedQuantity.ToString(),
            ["Counted"] = includeDifference ? line.CountedQuantity?.ToString() ?? "" : "",
            ["Difference"] = includeDifference ? line.Difference?.ToString() ?? "" : ""
        };

    private static async Task<InventoryCount> LoadCount(InventoryDbContext db, Guid id,
        CancellationToken ct) =>
        await db.InventoryCounts.AsNoTracking().Include(x => x.Location)
            .Include(x => x.Lines).ThenInclude(x => x.ProductVariant)
            .ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Inventory count", id);

    private static async Task<InventoryDocumentTemplate> LoadTemplate(InventoryDbContext db,
        Guid id, InventoryDocumentType type, CancellationToken ct)
    {
        var template = await db.InventoryDocumentTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Inventory document template", id);
        if (!template.IsActive || template.Type != type)
            throw new ConflictException("Template is inactive or has the wrong document type.");
        return template;
    }

    private static IResult Print(HttpContext http, string html, string filename)
    {
        http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
        http.Response.Headers.ContentDisposition = $"inline; filename={filename}";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static async Task SaveAsync(InventoryDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("An inventory document template with this name already exists.");
        }
    }
}

public sealed record CreateInventoryDocumentTemplateRequest(string Name, string Type,
    string Title, IReadOnlyList<string> Columns, string PaperSize,
    bool Landscape, string? FooterText);
public sealed record UpdateInventoryDocumentTemplateRequest(string Name, string Title,
    IReadOnlyList<string> Columns, string PaperSize, bool Landscape,
    string? FooterText);
