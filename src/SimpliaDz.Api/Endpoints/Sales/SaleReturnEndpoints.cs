using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Crm.UseCases.Loyalty;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Sales;

public static class SaleReturnEndpoints
{
    public static IEndpointRouteBuilder MapSaleReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sales")
            .WithTags("Sale Returns")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"));

        group.MapPost("/{saleId:guid}/returns", async (Guid saleId,
            [FromBody] ProcessSaleReturnRequest request, ClaimsPrincipal principal,
            [FromServices] ISaleReturnService returns, [FromServices] ITenantContext tenant,
            [FromServices] ILoyaltyService loyalty, [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.Id == Guid.Empty) errors["id"] = ["Return ID is required for safe retries."];
            if (request.LocationId == Guid.Empty) errors["locationId"] = ["Location is required."];
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
                errors["reason"] = ["Reason is required and cannot exceed 500 characters."];
            if (request.Lines is null || request.Lines.Count == 0)
                errors["lines"] = ["At least one return line is required."];
            else
            {
                if (request.Lines.Any(x => x.OriginalSaleLineId == Guid.Empty || x.Quantity <= 0))
                    errors["lines"] = ["Each line needs an original sale line and positive quantity."];
                else if (request.Lines.Select(x => x.OriginalSaleLineId).Distinct().Count() != request.Lines.Count)
                    errors["lines"] = ["Each original sale line may appear only once."];
            }
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var actor = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id : Guid.Empty;
            if (actor == Guid.Empty) throw new UnauthorizedAccessException("Authenticated user ID is required.");
            var result = await returns.ProcessAsync(request.Id, saleId, request.LocationId,
                request.Reason, actor, request.Lines!, ct);
            if (tenant.IsModuleEnabled("Crm"))
            {
                try { await loyalty.SyncSaleAsync(saleId, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    loggerFactory.CreateLogger("ReturnLoyaltySync").LogError(ex,
                        "Loyalty sync failed for return {ReturnId}; CRM read or sync can retry.", result.Id);
                }
            }
            return Results.Created($"/returns/{result.Id}", result);
        }).RequireAuthorization(PermissionNames.Sales.Manage,
            PermissionNames.SaleReturns.Manage);

        group.MapGet("/{saleId:guid}/returns", async (Guid saleId,
            [FromServices] ISaleReturnService returns, CancellationToken ct) =>
            Results.Ok(await returns.ListBySaleAsync(saleId, ct)))
            .RequireAuthorization(PermissionNames.Sales.View);

        app.MapGet("/returns/{id:guid}", async (Guid id,
            [FromServices] ISaleReturnService returns, CancellationToken ct) =>
            Results.Ok(await returns.GetByIdAsync(id, ct)))
            .WithTags("Sale Returns")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"))
            .RequireAuthorization(PermissionNames.Sales.View);
        return app;
    }
}

public sealed record ProcessSaleReturnRequest(Guid Id, Guid LocationId,
    string Reason, IReadOnlyList<ReturnSaleLine> Lines);
