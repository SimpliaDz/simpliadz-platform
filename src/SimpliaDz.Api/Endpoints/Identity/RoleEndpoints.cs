using MediatR;

using Microsoft.AspNetCore.Mvc;

using SimpliaDz.Identity.Contracts.Roles;
using SimpliaDz.Identity.UseCases.Roles.Commands.AssignPermissionsToRole;
using SimpliaDz.Identity.UseCases.Roles.Commands.CreateRole;
using SimpliaDz.Identity.UseCases.Roles.Commands.RemovePermissionsFromRole;
using SimpliaDz.Identity.UseCases.Roles.Queries.GetRoleById;
using SimpliaDz.Identity.UseCases.Roles.Queries.GetRoles;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Identity;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/roles")
            .WithTags("Roles")
            .WithMetadata(new ModuleEndpointMetadata("Identity"));

        group.MapGet("/", async (
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRolesQuery(), ct);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionNames.Roles.View);

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRoleByIdQuery(id), ct);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionNames.Roles.View);

        group.MapPost("/", async (
            [FromBody] CreateRoleRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateRoleCommand(request.Name), ct);
            return Results.Created($"/roles/{id}", new { id });
        }).RequireAuthorization(PermissionNames.Roles.Manage);

        group.MapPost("/{id:guid}/permissions", async (
            Guid id,
            [FromBody] AssignPermissionsToRoleRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new AssignPermissionsToRoleCommand(id, request.Permissions), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Roles.Manage);

        group.MapDelete("/{id:guid}/permissions", async (
            Guid id,
            [FromBody] RemovePermissionsFromRoleRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new RemovePermissionsFromRoleCommand(id, request.Permissions), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Roles.Manage);

        return app;
    }
}
