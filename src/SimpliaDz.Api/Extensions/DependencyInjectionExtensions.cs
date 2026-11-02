using FluentValidation;
using Mediator;

using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Mediation;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Resolution;
using SimpliaDz.SharedKernel.Authorization;
using SimpliaDz.Purchasing.UseCases.Services;

namespace SimpliaDz.Api.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddTenancyFromConfiguration(configuration);
        services.AddSingleton<ITenantResolver, ConfigurationTenantResolver>();
        services.AddHealthChecks();

        var modules = ModuleRegistry.GetModules();

        foreach (var module in modules)
        {
            // DbContexts resolve their connection string from the request-scoped ITenantContext.
            module.Register(services, string.Empty);
        }

        services.AddScoped<IInventoryReferenceValidator, InventoryReferenceValidator>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();

        var useCaseAssemblies = new[]
        {
            typeof(SimpliaDz.Inventory.UseCases.AssemblyMarker).Assembly,
            typeof(SimpliaDz.Identity.UseCases.AssemblyMarker).Assembly,
            typeof(SimpliaDz.Organization.UseCases.AssemblyMarker).Assembly
        };

        services.AddMediator(options =>
        {
            // Handlers depend on request-scoped repositories and DbContexts.
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies =
            [
                typeof(SimpliaDz.Inventory.UseCases.AssemblyMarker),
                typeof(SimpliaDz.Identity.UseCases.AssemblyMarker),
                typeof(SimpliaDz.Organization.UseCases.AssemblyMarker)
            ];
        });

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
