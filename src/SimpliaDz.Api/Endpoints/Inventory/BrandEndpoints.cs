using Mediator;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Inventory.Contracts.Brands;
using SimpliaDz.Inventory.UseCases.Brands.Commands.CreateBrand;
using SimpliaDz.Inventory.UseCases.Brands.Commands.UpdateBrand;
using SimpliaDz.Inventory.UseCases.Brands.Commands.DeactivateBrand;
using SimpliaDz.Inventory.UseCases.Brands.Queries.GetBrand;
using SimpliaDz.Inventory.UseCases.Brands.Queries.GetBrands;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Inventory;

public static class BrandEndpoints
{
    public static IEndpointRouteBuilder MapBrandEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/brands")
            .WithTags("Brands")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async ([FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetBrandsQuery(), ct)))
            .RequireAuthorization(PermissionNames.Brands.View);

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetBrandQuery(id), ct)))
            .RequireAuthorization(PermissionNames.Brands.View);

        group.MapPost("/", async ([FromBody] BrandRequest request, [FromServices] ISender sender, CancellationToken ct) =>
        {
            var brand = await sender.Send(new CreateBrandCommand(request.Name), ct);
            return Results.Created($"/brands/{brand.Id}", brand);
        }).RequireAuthorization(PermissionNames.Brands.Create);

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] BrandRequest request,
            [FromServices] ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateBrandCommand(id, request.Name), ct)))
            .RequireAuthorization(PermissionNames.Brands.Edit);

        group.MapPut("/{id:guid}/deactivate", async (Guid id, [FromServices] ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateBrandCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Brands.Edit);

        return app;
    }
}
