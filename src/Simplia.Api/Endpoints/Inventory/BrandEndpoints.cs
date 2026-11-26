using Mediator;
using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Inventory.Contracts.Brands;
using Simplia.Inventory.UseCases.Brands.Commands.CreateBrand;
using Simplia.Inventory.UseCases.Brands.Commands.UpdateBrand;
using Simplia.Inventory.UseCases.Brands.Commands.DeactivateBrand;
using Simplia.Inventory.UseCases.Brands.Commands.ActivateBrand;
using Simplia.Inventory.UseCases.Brands.Queries.GetBrand;
using Simplia.Inventory.UseCases.Brands.Queries.GetBrands;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Inventory;

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

        group.MapPut("/{id:guid}/activate", async (Guid id, [FromServices] ISender sender, CancellationToken ct) =>
        { await sender.Send(new ActivateBrandCommand(id), ct); return Results.NoContent(); })
            .RequireAuthorization(PermissionNames.Brands.Edit);

        return app;
    }
}
