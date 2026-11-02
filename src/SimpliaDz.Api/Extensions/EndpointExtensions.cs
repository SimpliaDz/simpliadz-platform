using SimpliaDz.Api.Endpoints.Identity;
using SimpliaDz.Api.Endpoints.Inventory;
using SimpliaDz.Api.Endpoints.Organization;

namespace SimpliaDz.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new { status = "ok" }));
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        app.MapAuthEndpoints();
        app.MapUserEndpoints();
        app.MapRoleEndpoints();
        app.MapPermissionEndpoints();
        app.MapProductEndpoints();
        app.MapCategoryEndpoints();
        app.MapBrandEndpoints();
        app.MapAttributeEndpoints();
        app.MapLocationEndpoints();
        app.MapOrganizationEndpoints();

        return app;
    }
}
