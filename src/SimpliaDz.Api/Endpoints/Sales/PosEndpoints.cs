using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
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
            ClaimsPrincipal principal, [FromServices] IPosService pos, CancellationToken ct) =>
            Results.Ok(await pos.AssignAsync(Actor(principal), registerId, ct)));

        group.MapDelete("/register-assignment", async (ClaimsPrincipal principal,
            [FromServices] IPosService pos, CancellationToken ct) =>
        {
            await pos.ReleaseAsync(Actor(principal), ct);
            return Results.NoContent();
        });

        group.MapPost("/sales", async ([FromBody] PosCreateSaleRequest request,
            ClaimsPrincipal principal, [FromServices] IPosService pos, [FromServices] ISaleService sales,
            CancellationToken ct) =>
        {
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.CreateDraftAsync(context.LocationId, request.CustomerId, actor,
                context.PriceListId, ct);
            return Results.Created($"/sales/{sale.Id}", sale);
        });

        group.MapPost("/sales/{saleId:guid}/scan", async (Guid saleId,
            [FromBody] PosScanRequest request, ClaimsPrincipal principal, [FromServices] IPosService pos,
            [FromServices] ISaleService sales, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 64)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["code"] = ["Scan code must contain 1 to 64 characters."] });
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != actor || sale.LocationId != context.LocationId || sale.Status != "Draft")
                return Results.Conflict("This ticket is not available at the current register.");
            try
            {
                return Results.Ok(await sales.AddLineAsync(saleId, null, request.Code.Trim(), 1, 0, ct));
            }
            catch (SimpliaDz.BuildingBlocks.Exceptions.NotFoundException)
            {
                return Results.Ok(await sales.AddLineAsync(saleId, request.Code.Trim(), null, 1, 0, ct));
            }
        });

        group.MapPut("/sales/{saleId:guid}/lines/{lineId:guid}/quantity", async (Guid saleId,
            Guid lineId, [FromBody] PosSetLineQuantityRequest request, ClaimsPrincipal principal,
            [FromServices] IPosService pos, [FromServices] ISaleService sales,
            CancellationToken ct) =>
        {
            if (request.Quantity <= 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["quantity"] = ["Quantity must be greater than zero."] });
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var sale = await sales.GetByIdAsync(saleId, ct);
            if (sale.CashierId != actor || sale.LocationId != context.LocationId || sale.Status != "Draft")
                return Results.Conflict("This ticket is not available at the current register.");
            return Results.Ok(await sales.SetLineQuantityAsync(saleId, lineId, request.Quantity, ct));
        });

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
        });

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
                if (!tenant.IsModuleEnabled("Crm"))
                    return Results.Ok(results.Select(x => new PosCustomerDto(x.Id, x.Name, x.Phone, x.Email)));
                var card = await loyalty.GetCardByNumberAsync(search.Trim(), ct);
                var cardCustomer = await customers.GetByIdAsync(card.CustomerId, ct);
                if (cardCustomer.IsActive && results.All(x => x.Id != cardCustomer.Id))
                    results.Insert(0, cardCustomer);
            }
            catch (NotFoundException) { }
            catch (ConflictException) { }
            return Results.Ok(results.Select(x => new PosCustomerDto(x.Id, x.Name, x.Phone, x.Email)));
        });

        group.MapPost("/sales/{saleId:guid}/pay", async (Guid saleId,
            [FromBody] PosPaymentRequest request, ClaimsPrincipal principal, [FromServices] IPosService pos,
            [FromServices] ISaleService sales, [FromServices] IPaymentService payments, CancellationToken ct) =>
        {
            if (request.Amount <= 0 || request.Id == Guid.Empty || !Enum.TryParse<PaymentMethod>(
                    request.PaymentMethod, true, out var method))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["payment"] = ["A valid payment ID, method, and positive amount are required."] });
            var actor = Actor(principal);
            var context = await pos.RequireContextAsync(actor, ct);
            var current = await sales.GetByIdAsync(saleId, ct);
            if (current.CashierId != actor || current.LocationId != context.LocationId)
                return Results.Conflict("This ticket is not available at the current register.");
            var completed = current.Status == "Draft"
                ? await sales.CompleteAsync(saleId, actor, ct)
                : current;
            var payment = await payments.RecordAsync(request.Id, saleId, request.Amount, method, actor, ct,
                context.CashSessionId);
            return Results.Ok(new PosPaymentResult(completed, payment));
        });

        return app;
    }

    private static Guid Actor(ClaimsPrincipal principal) => Guid.TryParse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty ? id :
        throw new UnauthorizedAccessException("Authenticated user ID is required.");
}

public sealed record PosCreateSaleRequest(Guid? CustomerId);
public sealed record PosScanRequest(string Code);
public sealed record PosSetLineQuantityRequest(int Quantity);
public sealed record PosSetCustomerRequest(Guid? CustomerId);
public sealed record PosCustomerDto(Guid Id, string Name, string? Phone, string? Email);
public sealed record PosPaymentRequest(Guid Id, decimal Amount, string PaymentMethod);
public sealed record PosPaymentResult(SaleDto Sale, PaymentDto Payment);
