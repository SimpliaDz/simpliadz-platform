using System.Security.Claims;

using Mediator;

using Microsoft.AspNetCore.Mvc;

using Simplia.Audit.UseCases;
using Simplia.Identity.Contracts.Auth;
using Simplia.Identity.UseCases.Auth.Abstractions;
using Simplia.Identity.UseCases.Auth.Commands.ChangePassword;
using Simplia.Identity.UseCases.Auth.Commands.ForgotPassword;
using Simplia.Identity.UseCases.Auth.Commands.Login;
using Simplia.Identity.UseCases.Auth.Commands.Logout;
using Simplia.Identity.UseCases.Auth.Commands.RefreshToken;
using Simplia.Identity.UseCases.Auth.Commands.ResetPassword;
using Simplia.Identity.UseCases.Users.Commands.UpdateLanguage;
using Simplia.Identity.UseCases.Users.Queries.GetUserById;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Identity;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Identity"));

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            HttpContext http,
            [FromServices] ISender sender,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(request.Email, request.Password,
                http.Connection.RemoteIpAddress?.ToString(),
                http.Request.Headers.UserAgent.ToString()), ct);
            if (tenant.IsModuleEnabled("Audit"))
            {
                var audit = http.RequestServices.GetRequiredService<IAuditTrail>();
                await audit.AppendAsync(result.UserId, "UserLoggedIn", "User",
                    result.UserId, null,
                    new Dictionary<string, object?> { ["method"] = "Password" },
                    http.TraceIdentifier, ct);
            }
            return Results.Ok(result);
        });

        group.MapGet("/login-attempts", async (Guid? userId, string? email,
            bool? succeeded, DateTimeOffset? from, DateTimeOffset? to,
            int? page, int? pageSize,
            [FromServices] ILoginAttemptReader reader, CancellationToken ct) =>
            Results.Ok(await reader.QueryAsync(userId, email, succeeded, from, to,
                page ?? 1, pageSize ?? 50, ct)))
            .RequireAuthorization(PermissionNames.LoginAttempts.View);

        group.MapGet("/me/login-attempts", async (ClaimsPrincipal principal,
            bool? succeeded, DateTimeOffset? from, DateTimeOffset? to,
            int? page, int? pageSize,
            [FromServices] ILoginAttemptReader reader, CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(await reader.QueryAsync(userId, null, succeeded,
                from, to, page ?? 1, pageSize ?? 50, ct));
        }).RequireAuthorization();

        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new RefreshTokenCommand(request.RefreshToken), ct);
            return Results.Ok(result);
        });

        // This endpoint intentionally does not require a valid access token: a client
        // must still be able to revoke its refresh token after the access token expires.
        group.MapPost("/logout", async (
            [FromBody] LogoutRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new LogoutCommand(request.RefreshToken), ct);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal,
            [FromServices] ISender sender, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue(ClaimTypes.Email);
            var roles = principal.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();
            var permissions = principal.FindAll("permission").Select(c => c.Value).ToArray();
            var user = await sender.Send(new GetUserByIdQuery(Guid.Parse(userId!)), ct);

            var displayName = string.Join(' ', new[] { user.FirstName, user.LastName }
                .Where(name => !string.IsNullOrWhiteSpace(name)));
            return Results.Ok(new { userId, email, roles, permissions, languageCode = user.LanguageCode,
                displayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName });
        }).RequireAuthorization();

        group.MapPut("/me/language", async (
            [FromBody] UpdateLanguageRequest request,
            ClaimsPrincipal principal,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await sender.Send(new UpdateLanguageCommand(userId, request.LanguageCode), ct);
            return Results.Ok(new { user.LanguageCode });
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
