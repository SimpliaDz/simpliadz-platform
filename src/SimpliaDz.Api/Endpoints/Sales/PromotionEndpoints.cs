using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Sales;

public static class PromotionEndpoints
{
    public static void MapPromotionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sales/promotions")
            .WithMetadata(new TenantRequiredMetadata(), new ModuleEndpointMetadata("Sales"))
            .RequireAuthorization(PermissionNames.Sales.Manage, PermissionNames.SaleDiscounts.Apply);
        group.MapGet("/", async (SalesDbContext db, CancellationToken ct) =>
            (await db.Promotions.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)).Select(ToResponse));
        group.MapGet("/targets", async (string target, string? search, InventoryDbContext db, CancellationToken ct) =>
        {
            var term = search?.Trim() ?? "";
            var kind = ParseTarget(target);
            var pattern = $"%{term}%";
            if (kind == PromotionTarget.Variant)
                return await db.Database.SqlQuery<PromotionOption>($"""
                    SELECT TOP (30) v.Id, CONCAT(p.Name, N' · ', v.Sku) AS Label
                    FROM inventory.ProductVariants v JOIN inventory.Products p ON p.Id = v.ProductId
                    WHERE p.Name LIKE {pattern} OR v.Sku LIKE {pattern} OR v.Barcode LIKE {pattern}
                    ORDER BY p.Name, v.Sku
                    """).ToListAsync(ct);
            if (kind == PromotionTarget.Product)
                return await db.Database.SqlQuery<PromotionOption>($"""
                    SELECT TOP (30) Id, Name AS Label FROM inventory.Products
                    WHERE Name LIKE {pattern} ORDER BY Name, Id
                    """).ToListAsync(ct);
            IQueryable<PromotionOption> options = kind switch
            {
                PromotionTarget.Category => db.Categories.Where(x => x.Name.Contains(term))
                    .Select(x => new PromotionOption(x.Id, x.Name)),
                _ => db.Brands.Where(x => x.Name.Contains(term))
                    .Select(x => new PromotionOption(x.Id, x.Name)),
            };
            return await options.OrderBy(x => x.Label).Take(30).ToListAsync(ct);
        });
        group.MapPost("/", async ([FromBody] PromotionRequest request, HttpContext http,
            SalesDbContext db, InventoryDbContext inventory, CancellationToken ct) =>
        {
            var promotion = new SalePromotion();
            await Configure(promotion, request, inventory, ct);
            db.Promotions.Add(promotion);
            await db.SaveChangesAsync(ct);
            await Audit(http, "PromotionCreated", promotion, null);
            return Results.Created($"/sales/promotions/{promotion.Id}", ToResponse(promotion));
        }).WithMetadata(new ExplicitAuditMetadata());
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] PromotionRequest request, HttpContext http,
            SalesDbContext db, InventoryDbContext inventory, CancellationToken ct) =>
        {
            var promotion = await db.Promotions.FindAsync([id], ct) ?? throw new NotFoundException("Promotion", id);
            var before = Values(promotion);
            await Configure(promotion, request, inventory, ct);
            await db.SaveChangesAsync(ct);
            await Audit(http, "PromotionUpdated", promotion, before);
            return Results.Ok(ToResponse(promotion));
        }).WithMetadata(new ExplicitAuditMetadata());
    }

    private static async Task Configure(SalePromotion promotion, PromotionRequest request,
        InventoryDbContext inventory, CancellationToken ct)
    {
        var target = ParseTarget(request.Target);
        if (!Enum.TryParse<SaleDiscountType>(request.DiscountType, true, out var discount))
            throw new ArgumentException("Choose Percentage or FixedAmount.");
        var id = request.TargetId;
        var exists = target switch
        {
            PromotionTarget.Category => await inventory.Categories.AnyAsync(x => x.Id == id, ct),
            PromotionTarget.Brand => await inventory.Brands.AnyAsync(x => x.Id == id, ct),
            PromotionTarget.Product => await inventory.Products.AnyAsync(x => x.Id == id, ct),
            _ => await inventory.ProductVariants.AnyAsync(x => x.Id == id, ct),
        };
        if (!exists) throw new NotFoundException("Promotion target", id);
        promotion.Configure(request.Name, target, id, discount, request.Value,
            request.StartsAt, request.EndsAt, request.IsActive);
    }

    private static PromotionTarget ParseTarget(string target) =>
        Enum.TryParse<PromotionTarget>(target, true, out var type) && Enum.IsDefined(type)
            ? type : throw new ArgumentException("Choose Category, Brand, Product, or Variant.");

    private static object ToResponse(SalePromotion promotion) => new
    {
        promotion.Id, promotion.Name, Target = promotion.Target.ToString(), promotion.TargetId,
        DiscountType = promotion.DiscountType.ToString(), promotion.Value,
        promotion.StartsAt, promotion.EndsAt, promotion.IsActive
    };

    private static Dictionary<string, object?> Values(SalePromotion promotion) => new()
    {
        ["name"] = promotion.Name, ["target"] = promotion.Target.ToString(), ["targetId"] = promotion.TargetId,
        ["discountType"] = promotion.DiscountType.ToString(), ["value"] = promotion.Value,
        ["startsAt"] = promotion.StartsAt.ToString("O"), ["endsAt"] = promotion.EndsAt?.ToString("O"),
        ["isActive"] = promotion.IsActive
    };

    private static Task Audit(HttpContext http, string action, SalePromotion promotion,
        Dictionary<string, object?>? before)
    {
        if (!http.RequestServices.GetRequiredService<SimpliaDz.BuildingBlocks.Tenancy.Abstractions.ITenantContext>()
                .IsModuleEnabled("Audit")) return Task.CompletedTask;
        return BusinessAudit.RecordAsync(http.RequestServices.GetRequiredService<SimpliaDz.Audit.UseCases.IAuditTrail>(),
            http, action, "SalePromotion", promotion.Id, before, Values(promotion), CancellationToken.None);
    }

    private sealed record PromotionOption(Guid Id, string Label);
    public sealed record PromotionRequest(string Name, string Target, Guid TargetId,
        string DiscountType, decimal Value, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, bool IsActive);
}
