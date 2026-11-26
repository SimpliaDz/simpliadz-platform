using Mediator;

using Microsoft.AspNetCore.Mvc;

using Simplia.Identity.UseCases.Permissions.Queries.GetPermissions;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Identity;

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
