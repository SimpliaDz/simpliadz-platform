using MediatR;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Organization.Contracts;
using SimpliaDz.Organization.UseCases.Commands.UpdateOrganization;
using SimpliaDz.Organization.UseCases.Queries.GetOrganization;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Organization;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/organization")
            .WithTags("Organization")
            .WithMetadata(new ModuleEndpointMetadata("Organization"));

        group.MapGet("/", async ([FromServices] ISender sender, CancellationToken ct) =>
        {
            var organization = await sender.Send(new GetOrganizationQuery(), ct);
            return Results.Ok(organization);
        }).RequireAuthorization(PermissionNames.Organization.View);

        group.MapPut("/", async (
            [FromBody] UpdateOrganizationRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var organization = await sender.Send(
                new UpdateOrganizationCommand(
                    request.Name,
                    request.LegalName,
                    request.Phone,
                    request.Email,
                    request.Address,
                    request.CurrencyCode,
                    request.DefaultLanguage,
                    request.IsActive),
                ct);

            return Results.Ok(organization);
        }).RequireAuthorization(PermissionNames.Organization.Manage);

        return app;
    }
}
