/*
Purpose

JWT configuration
AddAuthentication
AddAuthorization
*/
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using SimpliaDz.Api.Configuration;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;

namespace SimpliaDz.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or too short (min 32 chars).");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.HttpContext.GetEndpoint()?.Metadata
                                .GetMetadata<TenantRequiredMetadata>() is null)
                        {
                            return Task.CompletedTask;
                        }

                        var tenant = context.HttpContext.RequestServices
                            .GetRequiredService<ITenantContext>();
                        var tokenTenantId = context.Principal?.FindFirst("tenant_id")?.Value;

                        if (!string.Equals(tokenTenantId, tenant.TenantId, StringComparison.Ordinal))
                        {
                            context.Fail("The access token does not belong to the requested tenant.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
