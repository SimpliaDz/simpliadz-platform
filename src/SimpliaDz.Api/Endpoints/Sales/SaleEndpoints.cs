using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Crm.UseCases.Loyalty;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Sales;

public static class SaleEndpoints
{
    public static IEndpointRouteBuilder MapSaleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sales")
            .WithTags("Sales")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"));

        group.MapGet("/", async (Guid? locationId, Guid? customerId, string? status, string? search,
            int? page, int? pageSize, [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (locationId == Guid.Empty) errors["locationId"] = ["Location ID must be valid."];
            if (customerId == Guid.Empty) errors["customerId"] = ["Customer ID must be valid."];
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (!string.IsNullOrWhiteSpace(status) &&
                (!Enum.TryParse<SaleStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed)))
                errors["status"] = ["Invalid sale status."];
            if (search?.Trim().Length > 150) errors["search"] = ["Search must fit 150 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await sales.ListAsync(locationId, customerId, status, search,
                selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Sales.View);

        group.MapGet("/history", async (Guid? locationId, Guid? customerId, string? status, string? search,
            DateTimeOffset? from, DateTimeOffset? until, int? page, int? pageSize,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            if (from >= until)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["dateRange"] = ["The end must be after the start."] });
            return Results.Ok(await sales.HistoryAsync(locationId, customerId, status, search,
                from, until, page ?? 1, pageSize ?? 50, ct));
        }).RequireAuthorization(PermissionNames.Sales.View);

        group.MapGet("/history/customers", async (string? search, [FromServices] ICustomerService customers,
            CancellationToken ct) =>
        {
            var matches = await customers.SearchAsync(search, null, null, 1, 25, ct);
            return Results.Ok(matches.Select(customer => new { customer.Id, customer.Name }));
        }).RequireAuthorization(PermissionNames.Sales.View);

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] ISaleService sales,
            CancellationToken ct) => Results.Ok(await sales.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.Sales.View);

        group.MapPost("/", async ([FromBody] CreateSaleRequest request,
            ClaimsPrincipal principal, [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.LocationId == Guid.Empty) errors["locationId"] = ["Location is required."];
            if (request.CustomerId == Guid.Empty) errors["customerId"] = ["Customer ID must be valid."];
            if (request.PriceListId == Guid.Empty) errors["priceListId"] = ["Price list is required."];
            if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 64)
                errors["code"] = ["Scan an item to start a sale."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var sale = await sales.StartWithItemAsync(request.LocationId, request.CustomerId,
                RequireActor(principal), request.PriceListId, request.Code!.Trim(), ct);
            return Results.Created($"/sales/{sale.Id}", sale);
        }).AuditSale("SaleCreated", null).RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapPost("/{id:guid}/lines", async (Guid id, [FromBody] AddSaleLineRequest request,
            ClaimsPrincipal principal, [FromServices] IAuthorizationService authorization,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Sku) == string.IsNullOrWhiteSpace(request.Barcode))
                errors["variant"] = ["Provide exactly one SKU or barcode."];
            if (request.Sku?.Trim().Length > 64) errors["sku"] = ["SKU cannot exceed 64 characters."];
            if (request.Barcode?.Trim().Length > 64)
                errors["barcode"] = ["Barcode cannot exceed 64 characters."];
            if (request.Quantity <= 0) errors["quantity"] = ["Quantity must be greater than zero."];
            if (request.DiscountAmount < 0 || request.DiscountAmount > 99999999999999.9999m ||
                decimal.Round(request.DiscountAmount, 4) != request.DiscountAmount)
                errors["discountAmount"] = ["Discount must be nonnegative and fit decimal(18,4)."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (request.DiscountAmount > 0 &&
                !(await authorization.AuthorizeAsync(principal,
                    PermissionNames.SaleDiscounts.Apply)).Succeeded)
                return Results.Forbid();
            return Results.Ok(await sales.AddLineAsync(id, request.Sku, request.Barcode,
                request.Quantity, request.DiscountAmount, ct));
        }).AuditSale("SaleItemAdded", "id").RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapPut("/{id:guid}/lines/{lineId:guid}/discount", async (Guid id, Guid lineId,
            [FromBody] ApplySaleDiscountRequest request, [FromServices] ISaleService sales,
            CancellationToken ct) =>
        {
            var errors = ValidateDiscount(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await sales.ApplyLineDiscountAsync(id, lineId,
                Enum.Parse<SaleDiscountType>(request.Type.Trim(), true), request.Value, ct));
        }).AuditSale("SaleLineDiscountChanged", "id").RequireAuthorization(PermissionNames.Sales.Manage,
            PermissionNames.SaleDiscounts.Apply);

        group.MapPut("/{id:guid}/discount", async (Guid id,
            [FromBody] ApplySaleDiscountRequest request, [FromServices] ISaleService sales,
            CancellationToken ct) =>
        {
            var errors = ValidateDiscount(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await sales.ApplySaleDiscountAsync(id,
                Enum.Parse<SaleDiscountType>(request.Type.Trim(), true), request.Value, ct));
        }).AuditSale("SaleDiscountChanged", "id").RequireAuthorization(PermissionNames.Sales.Manage,
            PermissionNames.SaleDiscounts.Apply);

        group.MapPost("/{id:guid}/complete", async (Guid id, ClaimsPrincipal principal,
            [FromServices] ISaleService sales, [FromServices] ITenantContext tenant,
            [FromServices] ILoyaltyService loyalty, [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var sale = await sales.CompleteAsync(id, RequireActor(principal), ct);
            if (tenant.IsModuleEnabled("Crm") && sale.CustomerId.HasValue)
            {
                try { await loyalty.SyncSaleAsync(sale.Id, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    loggerFactory.CreateLogger("SaleLoyaltySync").LogError(ex,
                        "Loyalty sync failed for sale {SaleId}; CRM read or sync can retry.", sale.Id);
                }
            }
            return Results.Ok(sale);
        }).AuditSale("SaleCompleted", "id")
            .RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapPost("/{id:guid}/cancel", async (Guid id,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            await sales.CancelAsync(id, ct);
            return Results.NoContent();
        }).RequireAudit().AuditSale("SaleCancelled", "id").RequireAuthorization(PermissionNames.Sales.Manage);

        return app;
    }

    private static Guid RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
        userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("Authenticated user ID is required for sales.");

    private static Dictionary<string, string[]> ValidateDiscount(ApplySaleDiscountRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var isNamedType = Enum.GetNames<SaleDiscountType>().Any(x =>
            string.Equals(x, request.Type?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!isNamedType)
            errors["type"] = ["Discount type must be FixedAmount or Percentage."];
        if (request.Value < 0 || request.Value > 99999999999999.9999m ||
            decimal.Round(request.Value, 4) != request.Value)
            errors["value"] = ["Discount value must be nonnegative and fit decimal(18,4)."];
        else if (isNamedType &&
            Enum.Parse<SaleDiscountType>(request.Type!.Trim(), true) == SaleDiscountType.Percentage &&
            request.Value > 100m)
            errors["value"] = ["Percentage must be between 0 and 100."];
        return errors;
    }
}

public sealed record CreateSaleRequest(Guid LocationId, Guid? CustomerId, Guid PriceListId, string? Code);
public sealed record AddSaleLineRequest(string? Sku, string? Barcode, int Quantity,
    decimal DiscountAmount);
public sealed record ApplySaleDiscountRequest(string Type, decimal Value);
