using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Inventory;

public static class InventoryWarningEndpoints
{
    public static IEndpointRouteBuilder MapInventoryWarningEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/inventory-warnings")
            .WithTags("Inventory Warnings")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/settings", async ([FromServices] InventoryDbContext db,
            CancellationToken ct) => Results.Ok(MapSettings(await db.InventoryWarningSettings
                .AsNoTracking().SingleAsync(ct))))
            .RequireAuthorization(PermissionNames.InventoryWarnings.View);

        group.MapPut("/settings", async ([FromBody] ConfigureInventoryWarningsRequest request,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var settings = await db.InventoryWarningSettings.SingleAsync(ct);
            settings.Configure(request.Enabled, request.LowStockEnabled,
                request.AgeingEnabled, request.IntervalMinutes, request.AgeThresholdDays);
            await db.SaveChangesAsync(ct);
            return Results.Ok(MapSettings(settings));
        }).RequireAuthorization(PermissionNames.InventoryWarnings.Manage);

        group.MapPost("/run", async ([FromServices] InventoryWarningScanService scan,
            CancellationToken ct) =>
            await scan.RunAsync(true, ct) ? Results.Ok(new { ran = true }) :
                Results.Conflict(new { message = "A warning scan is already running." }))
            .RequireAuthorization(PermissionNames.InventoryWarnings.Manage);

        group.MapGet("/", async (Guid? locationId, string? kind, bool? activeOnly,
            int? page, int? pageSize, [FromServices] InventoryDbContext db,
            CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            if (locationId == Guid.Empty || selectedPage < 1 ||
                selectedSize is < 1 or > 100 || selectedPage > int.MaxValue / selectedSize ||
                (kind is not null && !Enum.TryParse<InventoryWarningKind>(kind, true, out _)))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["filter"] = ["Location, kind, and pagination must be valid."] });
            var query = db.InventoryWarnings.AsNoTracking().AsQueryable();
            if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId.Value);
            if (activeOnly ?? true) query = query.Where(x => x.IsActive);
            if (kind is not null)
            {
                var parsed = Enum.Parse<InventoryWarningKind>(kind, true);
                if (!Enum.IsDefined(parsed))
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                        { ["kind"] = ["Kind must be LowStock or Ageing."] });
                query = query.Where(x => x.Kind == parsed);
            }
            var warnings = await query.OrderByDescending(x => x.LastDetectedAt)
                .ThenBy(x => x.Id).Skip((selectedPage - 1) * selectedSize)
                .Take(selectedSize).ToListAsync(ct);
            var ids = warnings.Select(x => x.ProductVariantId).Distinct().ToArray();
            var locationIds = warnings.Select(x => x.LocationId).Distinct().ToArray();
            var variantNames = await db.ProductVariants.AsNoTracking().Include(x => x.Product)
                .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var locations = await db.Locations.AsNoTracking()
                .Where(x => locationIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
            return Results.Ok(warnings.Select(x => new InventoryWarningDto(
                x.Id, x.Kind.ToString(), x.ProductVariantId,
                variantNames[x.ProductVariantId].Sku.Value,
                variantNames[x.ProductVariantId].Product.Name.Value,
                x.LocationId, locations[x.LocationId].Name,
                x.QuantityOnHand, x.ReorderPoint, x.StockEnteredAt,
                x.IsActive, x.FirstDetectedAt, x.LastDetectedAt, x.ResolvedAt)));
        }).RequireAuthorization(PermissionNames.InventoryWarnings.View);

        return app;
    }

    private static InventoryWarningSettingsDto MapSettings(InventoryWarningSettings x) =>
        new(x.Enabled, x.LowStockEnabled, x.AgeingEnabled, x.IntervalMinutes,
            x.AgeThresholdDays, x.LastRunAt, x.NextRunAt);
}

public sealed record ConfigureInventoryWarningsRequest(bool Enabled,
    bool LowStockEnabled, bool AgeingEnabled, int IntervalMinutes, int AgeThresholdDays);
public sealed record InventoryWarningSettingsDto(bool Enabled, bool LowStockEnabled,
    bool AgeingEnabled, int IntervalMinutes, int AgeThresholdDays,
    DateTimeOffset? LastRunAt, DateTimeOffset NextRunAt);
public sealed record InventoryWarningDto(Guid Id, string Kind, Guid ProductVariantId,
    string Sku, string ProductName, Guid LocationId, string LocationName,
    int QuantityOnHand, int? ReorderPoint, DateTimeOffset? StockEnteredAt,
    bool IsActive, DateTimeOffset FirstDetectedAt, DateTimeOffset LastDetectedAt,
    DateTimeOffset? ResolvedAt);
