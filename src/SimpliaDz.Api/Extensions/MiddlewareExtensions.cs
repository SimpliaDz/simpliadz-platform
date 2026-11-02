using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Inventory.Core.Exceptions;

namespace SimpliaDz.Api.Extensions;

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

        return app;
    }
}
