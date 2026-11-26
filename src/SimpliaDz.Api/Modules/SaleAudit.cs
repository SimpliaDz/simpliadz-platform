using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.UseCases.Services;

namespace SimpliaDz.Api.Modules;

internal static class SaleAudit
{
    public static RouteHandlerBuilder AuditSale(this RouteHandlerBuilder route, string action,
        string? saleIdParameter = "saleId") =>
        route.WithMetadata(new ExplicitAuditMetadata()).AddEndpointFilter(async (invocation, next) =>
        {
            var http = invocation.HttpContext;
            if (!Enabled(http)) return await next(invocation);
            var sales = http.RequestServices.GetRequiredService<ISaleService>();
            var before = saleIdParameter is not null &&
                Guid.TryParse(http.Request.RouteValues[saleIdParameter]?.ToString(), out var id)
                ? await sales.GetByIdAsync(id, http.RequestAborted)
                : null;
            var result = await next(invocation);
            if (result is not IStatusCodeHttpResult { StatusCode: >= 200 and < 300 })
                return result;
            var value = (result as IValueHttpResult)?.Value;
            var after = action != "SaleCompleted" ? value as SaleDto : null;
            after ??= before is null ? null :
                await sales.GetByIdAsync(before.Id, CancellationToken.None);
            if (after is not null)
                await RecordAsync(http, action, before, after, value as PaymentDto, value as SaleReturnDto);
            return result;
        });

    private static bool Enabled(HttpContext http) =>
        http.RequestServices.GetRequiredService<ITenantContext>().IsModuleEnabled("Audit");

    public static async Task RecordAsync(HttpContext http, string action, SaleDto? before, SaleDto after,
        PaymentDto? payment = null, SaleReturnDto? saleReturn = null)
    {
        if (!Enabled(http)) return;
        if (payment is not null && before?.PaidAmount == after.PaidAmount) return;
        if (saleReturn is not null && before is not null &&
            before.Lines.Sum(line => line.ReturnedQuantity) == after.Lines.Sum(line => line.ReturnedQuantity)) return;

        var db = http.RequestServices.GetRequiredService<SalesDbContext>();
        var actor = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var register = payment?.CashSessionId is Guid sessionId
            ? await db.CashSessions.AsNoTracking().Where(session => session.Id == sessionId)
                .Select(session => new { session.CashRegisterId, session.CashRegister.Code }).FirstOrDefaultAsync()
            : await db.CashRegisterAssignments.AsNoTracking()
                .Where(item => item.UserId == actor && item.ReleasedAt == null &&
                    item.CashRegister.LocationId == after.LocationId)
                .Select(item => new { item.CashRegisterId, item.CashRegister.Code }).FirstOrDefaultAsync();

        Dictionary<string, object?> Snapshot(SaleDto sale) => new()
        {
            ["saleNumber"] = sale.SaleNumber, ["status"] = sale.Status,
            ["customerId"] = sale.CustomerId, ["locationId"] = sale.LocationId,
            ["registerId"] = register?.CashRegisterId, ["registerCode"] = register?.Code,
            ["subtotal"] = sale.Subtotal, ["discountAmount"] = sale.DiscountAmount,
            ["discountType"] = sale.DiscountType, ["discountValue"] = sale.DiscountValue,
            ["total"] = sale.Total, ["paidAmount"] = sale.PaidAmount, ["balanceDue"] = sale.BalanceDue,
        };

        var values = Snapshot(after);
        if (payment is not null)
        {
            values["paymentId"] = payment.Id;
            values["paymentAmount"] = payment.Amount;
            values["paymentMethod"] = payment.PaymentMethod;
            values["paymentReference"] = payment.Reference;
            values["paidAt"] = payment.PaidAt;
            values["cashSessionId"] = payment.CashSessionId;
        }
        if (saleReturn is not null)
        {
            values["returnId"] = saleReturn.Id;
            values["returnLocationId"] = saleReturn.LocationId;
            values["refundTotal"] = saleReturn.RefundTotal;
            values["reason"] = saleReturn.Reason;
        }

        var audit = http.RequestServices.GetRequiredService<SimpliaDz.Audit.UseCases.IAuditTrail>();
        await BusinessAudit.RecordAsync(audit, http, action, "Sale", after.Id,
            before is null ? null : Snapshot(before), values, CancellationToken.None);

        var oldLines = before?.Lines.ToDictionary(line => line.Id) ?? [];
        var newLines = after.Lines.ToDictionary(line => line.Id);
        var refunds = saleReturn?.Lines.ToDictionary(line => line.OriginalSaleLineId);
        foreach (var lineId in oldLines.Keys.Union(newLines.Keys))
        {
            var oldLine = oldLines.GetValueOrDefault(lineId);
            var newLine = newLines.GetValueOrDefault(lineId);
            var oldValues = LineValues(oldLine);
            var newValues = LineValues(newLine);
            if (refunds?.GetValueOrDefault(lineId) is { } refund && newValues is not null)
            {
                newValues["refundQuantity"] = refund.Quantity;
                newValues["unitRefundAmount"] = refund.UnitRefundAmount;
                newValues["refundAmount"] = refund.RefundAmount;
                newValues["restocked"] = refund.Restock;
            }
            if (action is not ("SaleCompleted" or "SaleCancelled") &&
                oldValues is not null && newValues is not null && oldValues.SequenceEqual(newValues)) continue;
            await BusinessAudit.RecordAsync(audit, http, action, "Sale", after.Id,
                oldValues, newValues, CancellationToken.None);
        }
    }

    private static Dictionary<string, object?>? LineValues(SaleLineDto? line) => line is null ? null : new()
    {
        ["lineId"] = line.Id, ["variantId"] = line.ProductVariantId, ["sku"] = line.Sku,
        ["productName"] = line.ProductName, ["variantName"] = line.VariantName,
        ["quantity"] = line.Quantity, ["unitPrice"] = line.UnitPrice,
        ["discountAmount"] = line.DiscountAmount, ["discountType"] = line.DiscountType,
        ["discountValue"] = line.DiscountValue, ["lineTotal"] = line.LineTotal,
        ["returnedQuantity"] = line.ReturnedQuantity,
        ["promotionId"] = line.PromotionId, ["promotionName"] = line.PromotionName,
        ["promotionUnitDiscount"] = line.PromotionUnitDiscount,
    };
}
