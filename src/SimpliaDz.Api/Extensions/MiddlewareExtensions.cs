/*
Purpose

exception handling
swagger setup
authentication middleware
authorization middleware
*/
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

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

                if (exception is UnauthorizedAccessException uex)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Error = "Unauthorized",
                        Message = uex.Message
                    });
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    Error = "ServerError"
                });
            });
        });

        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}