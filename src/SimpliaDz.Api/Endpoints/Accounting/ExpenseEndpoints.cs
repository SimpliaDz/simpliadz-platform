using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.Accounting.UseCases.Services;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Accounting;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/expenses")
            .WithTags("Expenses")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Accounting"));

        group.MapGet("/", async (DateOnly? fromDate, DateOnly? toDate,
            Guid? expenseCategoryId, Guid? locationId, int? page, int? pageSize,
            [FromServices] IExpenseService expenses, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                errors["dateRange"] = ["From date cannot be after to date."];
            if (expenseCategoryId == Guid.Empty)
                errors["expenseCategoryId"] = ["Category ID must be valid."];
            if (locationId == Guid.Empty)
                errors["locationId"] = ["Location ID must be valid."];
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await expenses.ListAsync(fromDate, toDate,
                expenseCategoryId, locationId, selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Expenses.View);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] IExpenseService expenses, CancellationToken ct) =>
            Results.Ok(await expenses.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.Expenses.View);

        group.MapPost("/", async ([FromBody] ExpenseRequest request,
            ClaimsPrincipal principal, [FromServices] IExpenseService expenses,
            CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var expense = await expenses.CreateAsync(request.ExpenseCategoryId,
                request.LocationId, request.Amount, request.ExpenseDate!.Value,
                request.Description, RequireActor(principal), ct);
            return Results.Created($"/expenses/{expense.Id}", expense);
        }).RequireAuthorization(PermissionNames.Expenses.Manage);

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] ExpenseRequest request,
            [FromServices] IExpenseService expenses, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await expenses.UpdateAsync(id, request.ExpenseCategoryId,
                request.LocationId, request.Amount, request.ExpenseDate!.Value,
                request.Description, ct));
        }).RequireAuthorization(PermissionNames.Expenses.Manage);

        return app;
    }

    private static Guid RequireActor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
        id != Guid.Empty ? id :
            throw new UnauthorizedAccessException("Authenticated user ID is required for expenses.");

    private static Dictionary<string, string[]> Validate(ExpenseRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ExpenseCategoryId == Guid.Empty)
            errors["expenseCategoryId"] = ["Expense category is required."];
        if (request.LocationId == Guid.Empty)
            errors["locationId"] = ["Location ID must be valid when supplied."];
        if (request.Amount <= 0 || request.Amount > 99999999999999.9999m ||
            decimal.Round(request.Amount, 4) != request.Amount)
            errors["amount"] = ["Amount must be positive and fit decimal(18,4)."];
        if (request.ExpenseDate is null || request.ExpenseDate == default(DateOnly))
            errors["expenseDate"] = ["Expense date is required."];
        if (request.Description?.Trim().Length > 2000)
            errors["description"] = ["Description cannot exceed 2000 characters."];
        return errors;
    }
}

public sealed record ExpenseRequest(Guid ExpenseCategoryId, Guid? LocationId,
    decimal Amount, DateOnly? ExpenseDate, string? Description);
