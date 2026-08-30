using Mediator;

using Microsoft.AspNetCore.Mvc;

using SimpliaDz.Identity.UseCases.Permissions.Queries.GetPermissions;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Identity;

public static class PermissionEndpoints
{
    public static IEndpointRouteBuilder MapPermissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/permissions")
            .WithTags("Permissions")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Identity"));

        group.MapGet("/", async (
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPermissionsQuery(), ct);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionNames.Permissions.View);

        return app;
    }
}
