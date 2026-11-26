/*
Purpose

JWT configuration
AddAuthentication
*/
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using Simplia.Api.Configuration;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Identity.UseCases.Auth.Abstractions;

namespace Simplia.Api.Extensions;

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

        if (jwtOptions.ExpiresMinutes <= 0 || jwtOptions.RefreshTokenExpiresDays <= 0)
            throw new InvalidOperationException("JWT expiration settings must be greater than zero.");

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
                    OnTokenValidated = async context =>
                    {
                        if (context.HttpContext.GetEndpoint()?.Metadata
                                .GetMetadata<TenantRequiredMetadata>() is null)
                        {
                            return;
                        }

                        var tenant = context.HttpContext.RequestServices
                            .GetRequiredService<ITenantContext>();
                        var tokenTenantId = context.Principal?.FindFirst("tenant_id")?.Value;

                        if (!string.Equals(tokenTenantId, tenant.TenantId, StringComparison.Ordinal))
                        {
                            context.Fail("The access token does not belong to the requested tenant.");
                            return;
                        }

                        var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var securityStamp = context.Principal?.FindFirst("security_stamp")?.Value;

                        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrEmpty(securityStamp))
                        {
                            context.Fail("The access token is missing required session claims.");
                            return;
                        }

                        var validator = context.HttpContext.RequestServices
                            .GetRequiredService<IIdentityAccessTokenValidator>();

                        if (!await validator.IsValidAsync(userId, securityStamp, context.HttpContext.RequestAborted))
                            context.Fail("The access token is no longer valid.");
                    }
                };
            });

        return services;
    }
}
