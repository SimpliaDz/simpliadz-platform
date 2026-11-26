using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Simplia.Api.Modules;
using Simplia.Audit.UseCases;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Inventory.Core.Exceptions;

namespace Simplia.Api.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseAppMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                switch (exception)
                {
                    case UnauthorizedAccessException uex:
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { Error = "Unauthorized", Message = uex.Message });
                        return;

                    case NotFoundException nfex:
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsJsonAsync(new { Error = "NotFound", Message = nfex.Message });
                        return;

                    case ValidationException vex:
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            Error = "ValidationError",
                            Errors = vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
                        });
                        return;

                    case ConflictException cex:
                        context.Response.StatusCode = StatusCodes.Status409Conflict;
                        await context.Response.WriteAsJsonAsync(new { Error = "Conflict", Message = cex.Message });
                        return;

                    case InsufficientStockException stockException:
                        context.Response.StatusCode = StatusCodes.Status409Conflict;
                        await context.Response.WriteAsJsonAsync(new { Error = "InsufficientStock", Message = stockException.Message });
                        return;

                    case InvalidOperationException ioex:
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsJsonAsync(new { Error = "BadRequest", Message = ioex.Message });
                        return;

                    case ArgumentException aex:
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsJsonAsync(new { Error = "BadRequest", Message = aex.Message });
                        return;

                    default:
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsJsonAsync(new { Error = "ServerError" });
                        return;
                }
            });
        });

        app.UseRouting();

        // Resolve and validate the tenant before authentication or endpoint execution.
        // System endpoints without TenantRequiredMetadata do not touch tenant state.
        app.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            var tenantRequired = endpoint?.Metadata.GetMetadata<TenantRequiredMetadata>() is not null;

            if (!tenantRequired)
            {
                await next();
                return;
            }

            var tenant = context.RequestServices.GetRequiredService<ITenantContext>();
            var module = endpoint!.Metadata.GetMetadata<ModuleEndpointMetadata>();

            if (module is not null && !tenant.IsModuleEnabled(module.ModuleName))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });

        app.UseAuthentication();
        app.UseAuthorization();

        // Most business writes are small endpoint handlers. Recording them here prevents new
        // endpoints from silently bypassing audit coverage; endpoints with richer before/after
        // records opt out through RequireAudit().
        app.Use(async (context, next) =>
        {
            await next();

            if (!IsAuditableMutation(context)) return;
            var tenant = context.RequestServices.GetRequiredService<ITenantContext>();
            if (!tenant.IsModuleEnabled("Audit")) return;
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
                userId == Guid.Empty) return;

            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<ExplicitAuditMetadata>() is not null) return;
            var path = context.Request.Path.Value ?? string.Empty;
            var route = endpoint?.DisplayName ?? path;
            var entityId = EntityIdFromPath(path);
            var audit = context.RequestServices.GetRequiredService<IAuditTrail>();
            await audit.AppendAsync(userId, $"{context.Request.Method}Succeeded", "ApiMutation",
                entityId, null, new Dictionary<string, object?>
                {
                    ["path"] = path,
                    ["route"] = route.Length <= 300 ? route : route[..300],
                    ["statusCode"] = context.Response.StatusCode
                }, context.TraceIdentifier, context.RequestAborted);
        });

        return app;
    }

    private static bool IsAuditableMutation(HttpContext context)
    {
        if (context.Response.StatusCode is < 200 or >= 400) return false;
        if (context.GetEndpoint()?.Metadata.GetMetadata<TenantRequiredMetadata>() is null) return false;
        if (context.Request.Path.StartsWithSegments("/auth")) return false;
        return HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
    }

    private static Guid EntityIdFromPath(string path)
    {
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries).Reverse())
            if (Guid.TryParse(segment, out var id) && id != Guid.Empty) return id;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        var result = new Guid(bytes[..16]);
        return result == Guid.Empty ? Guid.Parse("00000000-0000-0000-0000-000000000001") : result;
    }
}
