using FluentValidation;
using MediatR;

using SimpliaDz.Api.Modules;
using SimpliaDz.Api.Tenancy;
using SimpliaDz.BuildingBlocks.Mediation;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddHttpContextAccessor();
        services.AddTenancyFromConfiguration(configuration);
        services.AddSingleton<ITenantResolver, HttpTenantResolver>();

        var modules = ModuleRegistry.GetModules();

        foreach (var module in modules)
        {
            // DbContexts resolve their connection string from the request-scoped ITenantContext.
            module.Register(services, string.Empty);
        }

        var useCaseAssemblies = new[]
        {
            typeof(SimpliaDz.Inventory.UseCases.AssemblyMarker).Assembly,
            typeof(SimpliaDz.Identity.UseCases.AssemblyMarker).Assembly,
            typeof(SimpliaDz.Organization.UseCases.AssemblyMarker).Assembly
        };

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(useCaseAssemblies));

        services.AddValidatorsFromAssemblies(useCaseAssemblies);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionNames.All)
            {
                options.AddPolicy(permission, policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireClaim("permission", permission));
            }
        });

        return services;
    }
}
