using MediatR;

using Microsoft.AspNetCore.Mvc;

using SimpliaDz.Identity.Contracts.Users;
using SimpliaDz.Identity.UseCases.Users.Commands.ActivateUser;
using SimpliaDz.Identity.UseCases.Users.Commands.AssignRolesToUser;
using SimpliaDz.Identity.UseCases.Users.Commands.CreateUser;
using SimpliaDz.Identity.UseCases.Users.Commands.DeactivateUser;
using SimpliaDz.Identity.UseCases.Users.Commands.RemoveRolesFromUser;
using SimpliaDz.Identity.UseCases.Users.Queries.GetUserById;
using SimpliaDz.Identity.UseCases.Users.Queries.GetUserPermissions;
using SimpliaDz.Identity.UseCases.Users.Queries.GetUsers;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Identity;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
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
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new DeactivateUserCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Users.Edit);

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
