using Mediator;
using Microsoft.AspNetCore.Mvc;
using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Inventory.Contracts.Locations;
using Simplia.Inventory.UseCases.Locations.Commands.CreateLocation;
using Simplia.Inventory.UseCases.Locations.Commands.DeactivateLocation;
using Simplia.Inventory.UseCases.Locations.Commands.UpdateLocation;
using Simplia.Inventory.UseCases.Locations.Queries.GetLocation;
using Simplia.Inventory.UseCases.Locations.Queries.GetLocations;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Inventory;

public static class LocationEndpoints
{
    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/locations")
            .WithTags("Locations")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Inventory"));

        group.MapGet("/", async ([FromServices] ISender sender, CancellationToken ct) =>
        {
            var locations = await sender.Send(new GetLocationsQuery(), ct);
            return Results.Ok(locations);
        }).RequireAuthorization(PermissionNames.Locations.View);

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] ISender sender, CancellationToken ct) =>
        {
            var location = await sender.Send(new GetLocationQuery(id), ct);
            return Results.Ok(location);
        }).RequireAuthorization(PermissionNames.Locations.View);

        group.MapPost("/", async (
            [FromBody] CreateLocationRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var location = await sender.Send(new CreateLocationCommand(
                request.Name,
                request.Code,
                request.Type,
                request.Address,
                request.Phone), ct);

            return Results.Created($"/locations/{location.Id}", location);
        }).RequireAuthorization(PermissionNames.Locations.Create);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateLocationRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var location = await sender.Send(new UpdateLocationCommand(
                id,
                request.Name,
                request.Code,
                request.Type,
                request.Address,
                request.Phone), ct);

            return Results.Ok(location);
        }).RequireAuthorization(PermissionNames.Locations.Edit);

        group.MapPut("/{id:guid}/activate", async (Guid id, HttpContext http,
            [FromServices] InventoryDbContext db, CancellationToken ct) =>
        {
            var location = await db.Locations.FindAsync([id], ct)
                ?? throw new NotFoundException("Location", id);
            if (location.IsActive) return Results.NoContent();
            location.Activate();
            await db.SaveChangesAsync(ct);
            await BusinessAudit.RecordIfEnabledAsync(http, "LocationActivated", "Location", id,
                new Dictionary<string, object?> { ["name"] = location.Name, ["isActive"] = true }, ct);
            return Results.NoContent();
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization(PermissionNames.Locations.Edit);

        group.MapPut("/{id:guid}/deactivate", async (
            Guid id,
            HttpContext http,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new DeactivateLocationCommand(id), ct);
            await BusinessAudit.RecordIfEnabledAsync(http, "LocationDeactivated", "Location", id,
                new Dictionary<string, object?> { ["isActive"] = false }, ct);
            return Results.NoContent();
        }).WithMetadata(new ExplicitAuditMetadata()).RequireAuthorization(PermissionNames.Locations.Edit);

        return app;
    }
}
