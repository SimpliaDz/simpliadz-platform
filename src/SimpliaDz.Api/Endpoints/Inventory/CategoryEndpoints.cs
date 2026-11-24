using Mediator;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.Contracts.Categories;
using SimpliaDz.Inventory.UseCases.Categories.Commands.CreateCategory;
using SimpliaDz.Inventory.UseCases.Categories.Commands.DeactivateCategory;
using SimpliaDz.Inventory.UseCases.Categories.Commands.ActivateCategory;
using SimpliaDz.Inventory.UseCases.Categories.Commands.UpdateCategory;
using SimpliaDz.Inventory.UseCases.Categories.Queries.GetCategories;
using SimpliaDz.Inventory.UseCases.Categories.Queries.GetCategory;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories")
            .WithTags("Categories")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async ([FromServices] ISender sender, CancellationToken ct) =>
        {
            var categories = await sender.Send(new GetCategoriesQuery(), ct);
            return Results.Ok(categories);
        }).RequireAuthorization(PermissionNames.Categories.View);

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var category = await sender.Send(new GetCategoryQuery(id), ct);
            return Results.Ok(category);
        }).RequireAuthorization(PermissionNames.Categories.View);

        group.MapPost("/", async (
            [FromBody] CreateCategoryRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var category = await sender.Send(
                new CreateCategoryCommand(request.Name, request.ParentCategoryId, request.AttributeIds),
                ct);

            return Results.Created($"/categories/{category.Id}", category);
        }).RequireAuthorization(PermissionNames.Categories.Create);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateCategoryRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var category = await sender.Send(
                new UpdateCategoryCommand(id, request.Name, request.ParentCategoryId, request.AttributeIds),
                ct);

            return Results.Ok(category);
        }).RequireAuthorization(PermissionNames.Categories.Edit);

        group.MapPut("/{id:guid}/activate", async (Guid id, [FromServices] ISender sender, CancellationToken ct) =>
        { await sender.Send(new ActivateCategoryCommand(id), ct); return Results.NoContent(); })
            .RequireAuthorization(PermissionNames.Categories.Edit);

        group.MapPut("/{id:guid}/deactivate", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new DeactivateCategoryCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Categories.Edit);

        return app;
    }
}
