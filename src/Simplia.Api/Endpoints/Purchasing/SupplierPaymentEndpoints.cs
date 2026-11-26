using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Purchasing.Core.Entities;
using Simplia.Purchasing.Infrastructure.Data;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Purchasing;

public static class SupplierPaymentEndpoints
{
    public static IEndpointRouteBuilder MapSupplierPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/purchase-orders")
            .WithTags("Supplier Payments").WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Purchasing"));

        group.MapGet("/{orderId:guid}/payments", async (Guid orderId,
            [FromServices] PurchasingDbContext db, CancellationToken ct) =>
        {
            var order = await db.PurchaseOrders.AsNoTracking().Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == orderId, ct)
                ?? throw new NotFoundException("Purchase order", orderId);
            var payments = await db.SupplierPayments.AsNoTracking().Where(x => x.PurchaseOrderId == orderId)
                .OrderBy(x => x.PaymentDate).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).ToListAsync(ct);
            return Results.Ok(Balance(order, payments));
        }).RequireAuthorization(PermissionNames.PurchaseOrders.View);

        group.MapPost("/{orderId:guid}/payments", async (Guid orderId, SupplierPaymentRequest request,
            ClaimsPrincipal principal, HttpContext http, [FromServices] PurchasingDbContext db,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actor))
                return Results.Forbid();
            if (!Enum.TryParse<SupplierPaymentMethod>(request.Method, true, out var method))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["method"] = ["Select a valid payment method."] });
            var payment = new SupplierPayment(request.Id, orderId, request.Amount, method,
                request.PaymentDate, actor, request.Reference, request.Notes);
            var order = await db.PurchaseOrders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == orderId, ct)
                ?? throw new NotFoundException("Purchase order", orderId);
            var previous = await db.SupplierPayments.AsNoTracking().Where(x => x.PurchaseOrderId == orderId)
                .OrderBy(x => x.PaymentDate).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).ToListAsync(ct);
            if (previous.Any(x => x.Id == payment.Id))
                throw new ConflictException("Supplier payment has already been recorded.");
            var before = Balance(order, previous);
            try { order.ValidateSupplierPayment(payment.Amount, before.TotalPaid); }
            catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }

            db.SupplierPayments.Add(payment);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("The order changed concurrently. Reload its balance and retry.");
            }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
            {
                throw new ConflictException("Supplier payment has already been recorded.");
            }
            previous.Add(payment);
            var after = Balance(order, previous);
            await BusinessAudit.RecordIfEnabledAsync(http, "SupplierPaymentRecorded", "PurchaseOrder", orderId,
                new Dictionary<string, object?> { ["supplierId"] = order.SupplierId,
                    ["paymentId"] = payment.Id, ["amount"] = payment.Amount, ["method"] = payment.Method.ToString(),
                    ["paymentDate"] = payment.PaymentDate, ["reference"] = payment.Reference,
                    ["notes"] = payment.Notes, ["previousOutstanding"] = before.Outstanding,
                    ["outstanding"] = after.Outstanding }, CancellationToken.None);
            return Results.Ok(after);
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization(PermissionNames.PurchaseOrders.Manage);

        group.MapGet("/supplier-statement/{supplierId:guid}", async (Guid supplierId,
            [FromServices] PurchasingDbContext db, CancellationToken ct) =>
        {
            if (!await db.Suppliers.AnyAsync(x => x.Id == supplierId, ct))
                throw new NotFoundException("Supplier", supplierId);
            var orders = await db.PurchaseOrders.AsNoTracking().Include(x => x.Lines)
                .Where(x => x.SupplierId == supplierId && x.Status != PurchaseOrderStatus.Draft &&
                    x.Status != PurchaseOrderStatus.Cancelled).ToListAsync(ct);
            var payments = (await db.SupplierPayments.AsNoTracking()
                .Where(x => db.PurchaseOrders.Any(o => o.Id == x.PurchaseOrderId && o.SupplierId == supplierId))
                .ToListAsync(ct)).ToLookup(x => x.PurchaseOrderId);
            var balances = orders.Select(order => new SupplierStatementOrder(order.Id, order.PurchaseOrderNumber,
                order.Status.ToString(), order.OrderDate, Balance(order, payments[order.Id].ToArray()))).ToArray();
            var entries = orders.SelectMany(order =>
                payments[order.Id].Select(payment => new SupplierStatementEntry(order.Id, order.PurchaseOrderNumber,
                    payment.PaymentDate, payment.Id, -payment.Amount, payment.Reference))
                .Prepend(new(order.Id, order.PurchaseOrderNumber, DateOnly.FromDateTime(order.OrderDate.UtcDateTime),
                    null, order.Total, null)))
                .OrderBy(x => x.Date).ThenBy(x => x.PaymentId.HasValue).ThenBy(x => x.OrderId)
                .ThenBy(x => x.PaymentId).ToArray();
            decimal running = 0;
            return Results.Ok(new { outstanding = balances.Sum(x => x.Balance.Outstanding), orders = balances,
                entries = entries.Select(x => new { x.OrderId, x.OrderNumber, x.Date, x.PaymentId,
                    x.Change, x.Reference, running = running += x.Change }).ToArray() });
        }).RequireAuthorization(PermissionNames.PurchaseOrders.View);

        return app;
    }

    private static SupplierPaymentBalance Balance(PurchaseOrder order, IReadOnlyList<SupplierPayment> payments)
    {
        var paid = payments.Sum(x => x.Amount);
        var outstanding = order.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled ? 0 : order.Total - paid;
        return new(order.Total, paid, outstanding,
            order.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled ? "NotPayable" :
                outstanding == 0 ? "Paid" : paid == 0 ? "Unpaid" : "PartiallyPaid",
            payments.Select(x => new SupplierPaymentEntry(x.Id, x.Amount, x.Method.ToString(), x.PaymentDate,
                x.RecordedBy, x.Reference, x.Notes)).ToArray());
    }
}

public sealed record SupplierStatementOrder(Guid OrderId, string OrderNumber, string OrderStatus,
    DateTimeOffset OrderDate, SupplierPaymentBalance Balance);
public sealed record SupplierStatementEntry(Guid OrderId, string OrderNumber, DateOnly Date,
    Guid? PaymentId, decimal Change, string? Reference);
public sealed record SupplierPaymentRequest(Guid Id, decimal Amount, string Method,
    DateOnly PaymentDate, string? Reference, string? Notes);
public sealed record SupplierPaymentEntry(Guid Id, decimal Amount, string Method, DateOnly PaymentDate,
    Guid RecordedBy, string? Reference, string? Notes);
public sealed record SupplierPaymentBalance(decimal OrderTotal, decimal TotalPaid, decimal Outstanding,
    string Status, IReadOnlyList<SupplierPaymentEntry> Payments);
