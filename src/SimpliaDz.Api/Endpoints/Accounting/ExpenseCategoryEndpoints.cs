using Microsoft.AspNetCore.Mvc;
using SimpliaDz.Accounting.UseCases.Services;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Accounting;

public static class ExpenseCategoryEndpoints
{
    public static IEndpointRouteBuilder MapExpenseCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/expense-categories")
            .WithTags("Expense Categories")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Accounting"));

        group.MapGet("/", async (bool? isActive,
            [FromServices] IExpenseCategoryService categories, CancellationToken ct) =>
            Results.Ok(await categories.ListAsync(isActive, ct)))
            .RequireAuthorization(PermissionNames.ExpenseCategories.View);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] IExpenseCategoryService categories, CancellationToken ct) =>
            Results.Ok(await categories.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.ExpenseCategories.View);

        group.MapPost("/", async ([FromBody] ExpenseCategoryRequest request,
            [FromServices] IExpenseCategoryService categories, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var category = await categories.CreateAsync(request.Name, ct);
            return Results.Created($"/expense-categories/{category.Id}", category);
        }).RequireAuthorization(PermissionNames.ExpenseCategories.Manage);

        group.MapPut("/{id:guid}", async (Guid id,
            [FromBody] ExpenseCategoryRequest request,
            [FromServices] IExpenseCategoryService categories, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await categories.UpdateAsync(id, request.Name, ct));
        }).RequireAuthorization(PermissionNames.ExpenseCategories.Manage);

        group.MapPut("/{id:guid}/deactivate", async (Guid id,
            [FromServices] IExpenseCategoryService categories, CancellationToken ct) =>
        {
            await categories.DeactivateAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.ExpenseCategories.Manage);

        return app;
    }

    private static Dictionary<string, string[]> Validate(ExpenseCategoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
            errors["name"] = ["Expense category name is required."];
        else if (request.Name.Trim().Length > 150)
            errors["name"] = ["Expense category name cannot exceed 150 characters."];
        return errors;
    }
}

public sealed record ExpenseCategoryRequest(string Name);
