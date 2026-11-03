using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Sales;

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
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await payments.RecordAsync(request.Id, saleId, request.Amount,
                Enum.Parse<PaymentMethod>(request.PaymentMethod.Trim(), true),
                RequireActor(principal), ct);
            return Results.Created($"/payments/{result.Id}", result);
        }).RequireAuthorization(PermissionNames.Sales.Manage,
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

        return app;
    }

    private static Guid RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
        id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("Authenticated user ID is required for payments.");
}

public sealed record RecordPaymentRequest(Guid Id, decimal Amount, string PaymentMethod);
