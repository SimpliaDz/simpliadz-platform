using System.Security.Claims;

using MediatR;

using SimpliaDz.Identity.Contracts.Auth;
using SimpliaDz.Identity.UseCases.Auth.Commands.Login;

namespace SimpliaDz.Api.Endpoints.Identity;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (
            LoginRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new LoginCommand(request.Email, request.Password),
                ct);

            return Results.Ok(result);
        });

        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue(ClaimTypes.Email);
            var roles = principal.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();

            return Results.Ok(new { userId, email, roles });
        }).RequireAuthorization();

        return app;
    }
}