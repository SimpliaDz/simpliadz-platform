using SimpliaDz.Api.Endpoints.Identity;
using SimpliaDz.Api.Endpoints.Inventory;

namespace SimpliaDz.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new { status = "ok" }));

        app.MapAuthEndpoints();
        app.MapProductEndpoints();

        return app;
    }
}