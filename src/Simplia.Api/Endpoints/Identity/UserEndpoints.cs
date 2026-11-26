using Mediator;
using Simplia.Audit.UseCases;
using Simplia.Api.Modules;

using Microsoft.AspNetCore.Mvc;

using Simplia.Identity.Contracts.Users;
using Simplia.Identity.UseCases.Users.Commands.ActivateUser;
using Simplia.Identity.UseCases.Users.Commands.AssignRolesToUser;
using Simplia.Identity.UseCases.Users.Commands.CreateUser;
using Simplia.Identity.UseCases.Users.Commands.DeactivateUser;
using Simplia.Identity.UseCases.Users.Commands.RemoveRolesFromUser;
using Simplia.Identity.UseCases.Users.Queries.GetUserById;
using Simplia.Identity.UseCases.Users.Queries.GetUserPermissions;
using Simplia.Identity.UseCases.Users.Queries.GetUsers;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Identity;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Identity"));

        group.MapGet("/", async (
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUsersQuery(), ct);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionNames.Users.View);

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUserByIdQuery(id), ct);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionNames.Users.View);

        group.MapPost("/", async (
            [FromBody] CreateUserRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateUserCommand(request.Email, request.FirstName, request.LastName, request.Password, request.Roles),
                ct);
            return Results.Created($"/users/{id}", new { id });
        }).RequireAuthorization(PermissionNames.Users.Create);

        group.MapPost("/{id:guid}/roles", async (
            Guid id,
            [FromBody] AssignRolesToUserRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new AssignRolesToUserCommand(id, request.Roles), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Users.Edit);

        group.MapDelete("/{id:guid}/roles", async (
            Guid id,
            [FromBody] RemoveRolesFromUserRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new RemoveRolesFromUserCommand(id, request.Roles), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Users.Edit);

        group.MapPut("/{id:guid}/activate", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ActivateUserCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Users.Edit);

        group.MapPut("/{id:guid}/deactivate", async (
            Guid id,
            HttpContext http,
            [FromServices] ISender sender,
            [FromServices] IAuditTrail audit,
            CancellationToken ct) =>
        {
            var before = await sender.Send(new GetUserByIdQuery(id), ct);
            await sender.Send(new DeactivateUserCommand(id), ct);
            if (before.IsActive)
                await BusinessAudit.RecordAsync(audit, http, "UserDeactivated", "User", id,
                    new Dictionary<string, object?> { ["isActive"] = true },
                    new Dictionary<string, object?> { ["isActive"] = false }, ct);
            return Results.NoContent();
        }).RequireAudit().RequireAuthorization(PermissionNames.Users.Edit);

        group.MapGet("/{id:guid}/permissions", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUserPermissionsQuery(id), ct);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionNames.Users.View);

        return app;
    }
}
