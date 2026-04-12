using System.Security.Claims;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using SimpliaDz.Identity.Contracts.Auth;
using SimpliaDz.Identity.UseCases.Auth.Commands.ChangePassword;
using SimpliaDz.Identity.UseCases.Auth.Commands.ForgotPassword;
using SimpliaDz.Identity.UseCases.Auth.Commands.Login;
using SimpliaDz.Identity.UseCases.Auth.Commands.ResetPassword;

namespace SimpliaDz.Api.Endpoints.Identity;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(request.Email, request.Password), ct);
            return Results.Ok(result);
        });

        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue(ClaimTypes.Email);
            var roles = principal.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();
            var permissions = principal.FindAll("permission").Select(c => c.Value).ToArray();

            return Results.Ok(new { userId, email, roles, permissions });
        }).RequireAuthorization();

        group.MapPost("/change-password", async (
            [FromBody] ChangePasswordRequest request,
            ClaimsPrincipal principal,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await sender.Send(new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/forgot-password", async (
            [FromBody] ForgotPasswordRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new ForgotPasswordCommand(request.Email), ct);
            return Results.Ok(result);
        });

        group.MapPost("/reset-password", async (
            [FromBody] ResetPasswordRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ResetPasswordCommand(request.Email, request.ResetToken, request.NewPassword), ct);
            return Results.NoContent();
        });

        return app;
    }
}
