using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Crm.UseCases.Loyalty;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Sales;

public static class PosEndpoints
{
    private static Dictionary<string, object?> RegisterValues(string? code, string? name) =>
        new() { ["registerCode"] = code, ["registerName"] = name };

    public static IEndpointRouteBuilder MapPosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/pos")
            .WithTags("Point of Sale")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"))
            .RequireAuthorization(PermissionNames.Sales.Manage);

        group.MapGet("/context", async (ClaimsPrincipal principal, [FromServices] IPosService pos,
            CancellationToken ct) => Results.Ok(await pos.GetContextAsync(Actor(principal), ct)));

        group.MapPost("/registers/{registerId:guid}/assign", async (Guid registerId,
            ClaimsPrincipal principal, HttpContext http, [FromServices] IPosService pos,
            CancellationToken ct) =>
        {
            var context = await pos.AssignAsync(Actor(principal), registerId, ct);
            if (context.CashRegisterId == registerId)
                await BusinessAudit.RecordIfEnabledAsync(http, "CashRegisterSelected", "CashRegister",
                    registerId, RegisterValues(context.CashRegisterCode, context.CashRegisterName), ct);
            return Results.Ok(context);
        }).WithMetadata(new ExplicitAuditMetadata());

        group.MapDelete("/register-assignment", async (ClaimsPrincipal principal,
            HttpContext http, [FromServices] IPosService pos, CancellationToken ct) =>
        {
            var register = await pos.ReleaseAsync(Actor(principal), ct);
            if (register is not null)
                await BusinessAudit.RecordIfEnabledAsync(http, "CashRegisterReleased", "CashRegister",
                    register.Id, RegisterValues(register.Code, register.Name), ct);
            return Results.NoContent();
        }).WithMetadata(new ExplicitAuditMetadata());

        group.MapPost("/sales", async ([FromBody] PosCreateSaleRequest request,
            ClaimsPrincipal principal, [FromServices] IPosService pos, [FromServices] ISaleService sales,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 64)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["code"] = ["Scan an item to start a ticket."] });
            if (!CanUseStockOverride(principal, request.ConfirmPhysicalStock)) return Results.Forbid();
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.StartWithItemAsync(context.LocationId, request.CustomerId, actor,
                context.PriceListId, request.Code.Trim(), ct, checkStock: !request.ConfirmPhysicalStock);
            return Results.Created($"/sales/{sale.Id}", sale);
        }).AuditSale("SaleCreated", null);

        group.MapGet("/sales/{saleId:guid}", async (Guid saleId, ClaimsPrincipal principal,
            [FromServices] IPosService pos, [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != actor || sale.LocationId != context.LocationId ||
                sale.Status != "Draft" || sale.Lines.Count == 0)
                return Results.NotFound();
            return Results.Ok(sale);
        });

        group.MapDelete("/sales/{saleId:guid}/lines/{lineId:guid}", async (Guid saleId, Guid lineId,
            ClaimsPrincipal principal, [FromServices] IPosService pos,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != actor || sale.LocationId != context.LocationId || sale.Status != "Draft")
                return Results.NotFound();
            var updated = await sales.RemoveLineAsync(saleId, lineId, ct);
            return Results.Ok(updated);
        }).AuditSale("SaleLineRemoved");

        group.MapPost("/sales/{saleId:guid}/cancel", async (Guid saleId,
            ClaimsPrincipal principal, [FromServices] IPosService pos,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != actor || sale.LocationId != context.LocationId || sale.Status != "Draft")
                return Results.NotFound();
            await sales.CancelAsync(saleId, ct);
            return Results.NoContent();
        }).AuditSale("SaleCancelled");

        group.MapPost("/sales/{saleId:guid}/scan", async (Guid saleId,
            [FromBody] PosScanRequest request, ClaimsPrincipal principal, [FromServices] IPosService pos,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 64)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["code"] = ["Scan code must contain 1 to 64 characters."] });
            if (!CanUseStockOverride(principal, request.ConfirmPhysicalStock)) return Results.Forbid();
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != actor || sale.LocationId != context.LocationId || sale.Status != "Draft")
                return Results.Conflict("This ticket is not available at the current register.");
            if (sale.PriceListId != context.PriceListId)
                return Results.Conflict("This ticket does not use the current retail price list. Start a new POS ticket.");
            try
            {
                return Results.Ok(await sales.AddLineAsync(saleId, null, request.Code.Trim(), 1, 0, ct, checkStock: !request.ConfirmPhysicalStock));
            }
            catch (SimpliaDz.BuildingBlocks.Exceptions.NotFoundException)
            {
                return Results.Ok(await sales.AddLineAsync(saleId, request.Code.Trim(), null, 1, 0, ct, checkStock: !request.ConfirmPhysicalStock));
            }
        }).AuditSale("SaleItemAdded");

        group.MapPut("/sales/{saleId:guid}/lines/{lineId:guid}/quantity", async (Guid saleId,
            Guid lineId, [FromBody] PosSetLineQuantityRequest request, ClaimsPrincipal principal,
            [FromServices] IPosService pos, [FromServices] ISaleService sales,
            CancellationToken ct) =>
        {
            if (request.Quantity <= 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["quantity"] = ["Quantity must be greater than zero."] });
            if (!CanUseStockOverride(principal, request.ConfirmPhysicalStock)) return Results.Forbid();
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != actor || sale.LocationId != context.LocationId || sale.Status != "Draft")
                return Results.Conflict("This ticket is not available at the current register.");
            return Results.Ok(await sales.SetLineQuantityAsync(saleId, lineId, request.Quantity, ct, checkStock: !request.ConfirmPhysicalStock));
        }).AuditSale("SaleQuantityChanged");

        group.MapPut("/sales/{saleId:guid}/customer", async (Guid saleId,
            [FromBody] PosSetCustomerRequest request, ClaimsPrincipal principal,
            [FromServices] IPosService pos, [FromServices] ISaleService sales,
            CancellationToken ct) =>
        {
            if (request.CustomerId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["customerId"] = ["Customer ID must be valid."] });
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != actor || sale.LocationId != context.LocationId || sale.Status != "Draft")
                return Results.Conflict("This ticket is not available at the current register.");
            return Results.Ok(await sales.SetCustomerAsync(saleId, request.CustomerId, ct));
        }).AuditSale("SaleCustomerChanged");

        group.MapGet("/customers", async (string? search,
            [FromServices] ICustomerService customers, [FromServices] ILoyaltyService loyalty,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(search) || search.Trim().Length > 150)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["search"] = ["Enter 1 to 150 characters to find a customer."] });
            var results = (await customers.SearchAsync(search.Trim(), null, true, 1, 20, ct)).ToList();
            try
            {
                if (!tenant.IsModuleEnabled("Crm") || search.Trim().Length > 35)
                    return Results.Ok(results.Select(x => new PosCustomerDto(x.Id, x.Name, x.Phone, x.Email)));
                var card = Guid.TryParseExact(search.Trim(), "N", out var customerId)
                    ? await loyalty.GetCardByCustomerAsync(customerId, ct)
                    : await loyalty.GetCardByNumberAsync(search.Trim(), ct);
                if (!card.IsActive)
                    return Results.Ok(results.Select(x => new PosCustomerDto(x.Id, x.Name, x.Phone, x.Email)));
                var cardCustomer = await customers.GetByIdAsync(card.CustomerId, ct);
                if (cardCustomer.IsActive && results.All(x => x.Id != cardCustomer.Id))
                    results.Insert(0, cardCustomer);
            }
            catch (NotFoundException) { }
            catch (ConflictException) { }
            return Results.Ok(results.Select(x => new PosCustomerDto(x.Id, x.Name, x.Phone, x.Email)));
        });

        group.MapGet("/sales/{saleId:guid}/receipt.pdf", async (Guid saleId, ClaimsPrincipal principal,
            [FromServices] ISaleService sales, [FromServices] SalesPdfDocumentService documents,
            CancellationToken ct) =>
        {
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != Actor(principal)) return Results.NotFound();
            var pdf = await documents.ReceiptAsync(saleId, ct);
            return Results.File(pdf.Content, "application/pdf", pdf.FileName);
        });

        group.MapPost("/sales/{saleId:guid}/pay", async (Guid saleId,
            [FromBody] PosPaymentRequest request, ClaimsPrincipal principal, HttpContext http, [FromServices] IPosService pos,
            [FromServices] ISaleService sales, [FromServices] IPaymentService payments, CancellationToken ct) =>
        {
            if (request.Amount < 0 || request.Id == Guid.Empty || !Enum.TryParse<PaymentMethod>(
                    request.PaymentMethod, true, out var method))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["payment"] = ["A valid payment ID, method, and non-negative amount are required."] });
            if (!CanUseStockOverride(principal, request.ConfirmPhysicalStock)) return Results.Forbid();
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var current = await sales.GetByIdAsync(saleId, ct);
            if (current.CashierId != actor || current.LocationId != context.LocationId)
                return Results.Conflict("This ticket is not available at the current register.");
            if (current.Status == "Draft" && current.PriceListId != context.PriceListId)
                return Results.Conflict("This ticket does not use the current retail price list. Start a new POS ticket.");
            if (request.Amount == 0 && current.Total != 0)
                return Results.Conflict("Only a zero-total ticket can be completed without payment.");
            var completed = current.Status == "Draft"
                ? await sales.CompleteAsync(saleId, actor, ct, adjustShortfall: request.ConfirmPhysicalStock)
                : current;
            if (current.Status == "Draft")
                await SaleAudit.RecordAsync(http, "SaleCompleted", current, completed with { Lines = current.Lines });
            if (request.Amount == 0) return Results.Ok(new PosPaymentResult(completed, null));
            var payment = await payments.RecordAsync(request.Id, saleId, request.Amount, method, actor, ct,
                context.CashSessionId);
            await SaleAudit.RecordAsync(http, "SalePaymentRecorded", completed,
                await sales.GetByIdAsync(saleId, CancellationToken.None), payment);
            return Results.Ok(new PosPaymentResult(completed, payment));
        }).WithMetadata(new ExplicitAuditMetadata());

        return app;
    }

    private static bool CanUseStockOverride(ClaimsPrincipal principal, bool requested) =>
        !requested || principal.HasClaim("permission", PermissionNames.Stock.Manage);

    private static Guid Actor(ClaimsPrincipal principal) => Guid.TryParse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty ? id :
        throw new UnauthorizedAccessException("Authenticated user ID is required.");
}

public sealed record PosCreateSaleRequest(Guid? CustomerId, string? Code, bool ConfirmPhysicalStock = false);
public sealed record PosScanRequest(string Code, bool ConfirmPhysicalStock = false);
public sealed record PosSetLineQuantityRequest(int Quantity, bool ConfirmPhysicalStock = false);
public sealed record PosSetCustomerRequest(Guid? CustomerId);
public sealed record PosCustomerDto(Guid Id, string Name, string? Phone, string? Email);
public sealed record PosPaymentRequest(Guid Id, decimal Amount, string PaymentMethod, bool ConfirmPhysicalStock = false);
public sealed record PosPaymentResult(SaleDto Sale, PaymentDto? Payment);
