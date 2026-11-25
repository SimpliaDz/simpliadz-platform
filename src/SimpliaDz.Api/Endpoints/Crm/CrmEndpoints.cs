using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Organization.Infrastructure.Data;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Crm.UseCases.Loyalty;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Crm;

public static class CrmEndpoints
{
    public static IEndpointRouteBuilder MapCrmEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/crm/loyalty")
            .WithTags("CRM Loyalty")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Crm"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            return tenant.IsModuleEnabled("Sales")
                ? await next(context)
                : Results.Conflict(new { message = "CRM loyalty requires the Sales module." });
        });

        group.MapGet("/program", async ([FromServices] ILoyaltyService service,
            CancellationToken ct) => Results.Ok(await service.GetProgramAsync(ct)))
            .RequireAuthorization(PermissionNames.Crm.View);

        group.MapPut("/program", async ([FromBody] ConfigureLoyaltyProgramRequest request,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
                errors["name"] = ["Name is required and must fit 120 characters."];
            if (request.SpendAmountPerPoint <= 0 ||
                decimal.Round(request.SpendAmountPerPoint, 4) != request.SpendAmountPerPoint)
                errors["spendAmountPerPoint"] = ["Enter a positive DZD amount with at most four decimals."];
            if (request.IsActive is null)
                errors["isActive"] = ["Active state is required."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await service.ConfigureProgramAsync(request.Name,
                request.SpendAmountPerPoint, request.IsActive!.Value, ct));
        }).RequireAuthorization(PermissionNames.Crm.Manage);

        group.MapPost("/cards", async ([FromBody] EnrollLoyaltyCustomerRequest request,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
        {
            if (request.CustomerId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["customerId"] = ["Customer is required."] });
            var card = await service.EnrollAsync(request.CustomerId, ct);
            return Results.Created($"/crm/loyalty/cards/{card.Id}", card);
        }).RequireAuthorization(PermissionNames.Crm.Manage);

        group.MapGet("/customers/{customerId:guid}/card", async (Guid customerId,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
            Results.Ok(await service.GetCardByCustomerAsync(customerId, ct)))
            .RequireAuthorization(PermissionNames.Crm.View);

        group.MapGet("/customers/{customerId:guid}/card/{format:regex(^(pdf|svg)$)}", async (Guid customerId, string format,
            [FromServices] ILoyaltyService loyalty, [FromServices] ICustomerService customers,
            [FromServices] OrganizationDbContext organization, CancellationToken ct) =>
        {
            var card = await loyalty.GetCardByCustomerAsync(customerId, ct);
            var customer = await customers.GetByIdAsync(customerId, ct);
            var name = await LoyaltyCardDocument.OrganizationNameAsync(organization, ct);
            if (string.Equals(format, "svg", StringComparison.OrdinalIgnoreCase))
                return Results.Content(LoyaltyCardDocument.Artwork(customer.Name, customer.Id, card.CardNumber, name),
                    "image/svg+xml", System.Text.Encoding.UTF8);
            return Results.File(LoyaltyCardDocument.Render(customer.Name, customer.Id, card.CardNumber, name),
                "application/pdf", "carte-fidelite.pdf");
        }).RequireAuthorization(PermissionNames.Crm.Manage);

        group.MapPost("/customers/{customerId:guid}/card/email", async (Guid customerId,
            HttpContext http, [FromServices] ILoyaltyService loyalty, [FromServices] ICustomerService customers,
            [FromServices] OrganizationDbContext organization, [FromServices] IDataProtectionProvider protection,
            CancellationToken ct) =>
        {
            var card = await loyalty.GetCardByCustomerAsync(customerId, ct);
            var customer = await customers.GetByIdAsync(customerId, ct);
            if (!card.IsActive || !customer.IsActive || string.IsNullOrWhiteSpace(customer.Email))
                throw new ConflictException("Email requires an active customer and card and a customer email address.");
            await LoyaltyCardDocument.SendAsync(organization, protection, customer.Email,
                LoyaltyCardDocument.Render(customer.Name, customer.Id, card.CardNumber,
                    await LoyaltyCardDocument.OrganizationNameAsync(organization, ct)), ct);
            await BusinessAudit.RecordIfEnabledAsync(http, "LoyaltyCardEmailed", "LoyaltyCard", card.Id, null, ct);
            return Results.NoContent();
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization(PermissionNames.Crm.Manage);

        group.MapGet("/cards/{cardId:guid}", async (Guid cardId,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
            Results.Ok(await service.GetCardByIdAsync(cardId, ct)))
            .RequireAuthorization(PermissionNames.Crm.View);

        group.MapGet("/cards/by-number/{cardNumber}", async (string cardNumber,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
            Results.Ok(await service.GetCardByNumberAsync(cardNumber, ct)))
            .RequireAuthorization(PermissionNames.Crm.View);

        group.MapPut("/cards/{cardId:guid}/active", async (Guid cardId,
            [FromBody] SetLoyaltyActiveRequest request,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
            request.IsActive is null
                ? Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["isActive"] = ["Active state is required."] })
                : Results.Ok(await service.SetCardActiveAsync(cardId,
                    request.IsActive.Value, ct)))
            .RequireAuthorization(PermissionNames.Crm.Manage);

        group.MapGet("/cards/{cardId:guid}/transactions", async (Guid cardId,
            int? page, int? pageSize, [FromServices] ILoyaltyService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetHistoryAsync(cardId, page ?? 1,
                pageSize ?? 50, ct)))
            .RequireAuthorization(PermissionNames.Crm.View);

        group.MapGet("/rewards", async (bool? includeInactive,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
            Results.Ok(await service.ListRewardsAsync(includeInactive ?? false, ct)))
            .RequireAuthorization(PermissionNames.Crm.View);

        group.MapGet("/rewards/{id:guid}", async (Guid id,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
            Results.Ok(await service.GetRewardByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.Crm.View);

        group.MapPost("/rewards", async ([FromBody] CreateLoyaltyRewardRequest request,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
        {
            var errors = ValidateReward(request.Name, request.PointsCost);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var reward = await service.CreateRewardAsync(request.Name, request.PointsCost, ct);
            return Results.Created($"/crm/loyalty/rewards/{reward.Id}", reward);
        }).RequireAuthorization(PermissionNames.Crm.Manage);

        group.MapPut("/rewards/{id:guid}", async (Guid id,
            [FromBody] UpdateLoyaltyRewardRequest request,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
        {
            var errors = ValidateReward(request.Name, request.PointsCost);
            if (request.IsActive is null)
                errors["isActive"] = ["Active state is required."];
            return errors.Count > 0 ? Results.ValidationProblem(errors) :
                Results.Ok(await service.UpdateRewardAsync(id, request.Name,
                    request.PointsCost, request.IsActive!.Value, ct));
        }).RequireAuthorization(PermissionNames.Crm.Manage);

        group.MapPost("/cards/{cardId:guid}/redemptions", async (Guid cardId,
            [FromBody] RedeemLoyaltyRewardRequest request, ClaimsPrincipal principal,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.RewardId == Guid.Empty) errors["rewardId"] = ["Reward is required."];
            if (request.RedemptionId == Guid.Empty)
                errors["redemptionId"] = ["A redemption ID is required for safe retries."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await service.RedeemAsync(cardId, request.RewardId,
                request.RedemptionId, RequireActor(principal), ct));
        }).RequireAuthorization(PermissionNames.Crm.Redeem);

        group.MapPost("/sales/{saleId:guid}/sync", async (Guid saleId,
            [FromServices] ILoyaltyService service, CancellationToken ct) =>
            Results.Ok(await service.SyncSaleAsync(saleId, ct)))
            .RequireAuthorization(PermissionNames.Crm.Manage);

        return app;
    }

    private static Guid RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)
            && actor != Guid.Empty ? actor :
            throw new UnauthorizedAccessException("Authenticated user ID is required.");

    private static Dictionary<string, string[]> ValidateReward(string name, int pointsCost)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            errors["name"] = ["Reward name is required and must fit 120 characters."];
        if (pointsCost <= 0)
            errors["pointsCost"] = ["Points cost must be positive."];
        return errors;
    }
}

public sealed record ConfigureLoyaltyProgramRequest(string Name,
    decimal SpendAmountPerPoint, bool? IsActive);
public sealed record EnrollLoyaltyCustomerRequest(Guid CustomerId);
public sealed record SetLoyaltyActiveRequest(bool? IsActive);
public sealed record CreateLoyaltyRewardRequest(string Name, int PointsCost);
public sealed record UpdateLoyaltyRewardRequest(string Name, int PointsCost, bool? IsActive);
public sealed record RedeemLoyaltyRewardRequest(Guid RewardId, Guid RedemptionId);
