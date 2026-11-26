using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Sales.Infrastructure.Data;
using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Sales.Core.Entities;
using Simplia.Sales.UseCases.Services;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Sales;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sales")
            .WithTags("Sale Payments")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"));

        group.MapPost("/{saleId:guid}/payments", async (Guid saleId,
            [FromBody] RecordPaymentRequest request, ClaimsPrincipal principal,
            [FromServices] IPaymentService payments, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.Id == Guid.Empty) errors["id"] = ["Payment ID is required for safe retries."];
            if (request.Amount <= 0 || request.Amount > 99999999999999.9999m ||
                decimal.Round(request.Amount, 4) != request.Amount)
                errors["amount"] = ["Amount must be positive and fit decimal(18,4)."];
            if (!Enum.GetNames<PaymentMethod>().Any(x =>
                    string.Equals(x, request.PaymentMethod?.Trim(), StringComparison.OrdinalIgnoreCase)))
                errors["paymentMethod"] = ["Method must be Cash, Card, BankTransfer, or Other."];
            if (request.CashSessionId == Guid.Empty)
                errors["cashSessionId"] = ["Cash session ID must be valid."];
            if (request.Reference?.Trim().Length > 100)
                errors["reference"] = ["Payment reference cannot exceed 100 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await payments.RecordAsync(request.Id, saleId, request.Amount,
                Enum.Parse<PaymentMethod>(request.PaymentMethod.Trim(), true),
                RequireActor(principal), ct, request.CashSessionId, request.Reference);
            return Results.Created($"/payments/{result.Id}", result);
        }).AuditSale("SalePaymentRecorded").RequireAuthorization(PermissionNames.Sales.Manage,
            PermissionNames.SalePayments.Manage);

        group.MapGet("/{saleId:guid}/payments", async (Guid saleId,
            [FromServices] IPaymentService payments, CancellationToken ct) =>
            Results.Ok(await payments.ListBySaleAsync(saleId, ct)))
            .RequireAuthorization(PermissionNames.Sales.View);

        app.MapGet("/payments/{id:guid}", async (Guid id,
            [FromServices] IPaymentService payments, CancellationToken ct) =>
            Results.Ok(await payments.GetByIdAsync(id, ct)))
            .WithTags("Sale Payments")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"))
            .RequireAuthorization(PermissionNames.Sales.View);

        group.MapGet("/customers/{customerId:guid}/statement", async (Guid customerId,
            [FromServices] SalesDbContext db, CancellationToken ct) =>
        {
            if (!await db.Customers.AnyAsync(x => x.Id == customerId, ct))
                throw new NotFoundException("Customer", customerId);
            var orders = await db.Sales.AsNoTracking().Where(x => x.CustomerId == customerId &&
                (x.Status == SaleStatus.Completed || x.Status == SaleStatus.PartiallyReturned || x.Status == SaleStatus.Returned))
                .Select(x => new CustomerStatementSale(x.Id, x.SaleNumber, x.SaleDate, x.Total, x.PaidAmount, x.Status))
                .ToListAsync(ct);
            var payments = (await db.Payments.AsNoTracking().Where(x => x.Sale.CustomerId == customerId)
                .ToListAsync(ct)).ToLookup(x => x.SaleId);
            var entries = orders.SelectMany(sale => payments[sale.Id].Select(payment =>
                new CustomerStatementEntry(sale.Id, sale.Number, payment.PaidAt, payment.Id, -payment.Amount, payment.Reference))
                .Prepend(new(sale.Id, sale.Number, sale.Date, null, sale.Total, null)))
                .OrderBy(x => x.Date).ThenBy(x => x.PaymentId.HasValue).ThenBy(x => x.SaleId)
                .ThenBy(x => x.PaymentId).ToArray();
            decimal running = 0;
            return Results.Ok(new {
                outstanding = orders.Where(x => x.Status == SaleStatus.Completed).Sum(x => x.Total - x.Paid),
                openSales = orders.Where(x => x.Status == SaleStatus.Completed && x.Total > x.Paid).OrderBy(x => x.Date),
                entries = entries.Select(x => new { x.SaleId, x.Number, x.Date, x.PaymentId, x.Change, x.Reference,
                    runningBalance = running += x.Change }).ToArray()
            });
        }).RequireAuthorization(PermissionNames.Sales.View);

        return app;
    }

    private static Guid RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
        id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("Authenticated user ID is required for payments.");
}

public sealed record RecordPaymentRequest(Guid Id, decimal Amount, string PaymentMethod,
    Guid? CashSessionId = null, string? Reference = null);
public sealed record CustomerStatementSale(Guid Id, string? Number, DateTimeOffset Date,
    decimal Total, decimal Paid, SaleStatus Status);
public sealed record CustomerStatementEntry(Guid SaleId, string? Number, DateTimeOffset Date,
    Guid? PaymentId, decimal Change, string? Reference);
