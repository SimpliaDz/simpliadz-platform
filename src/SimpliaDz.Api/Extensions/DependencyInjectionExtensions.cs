using FluentValidation;
using MediatR;

using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Mediation;

namespace SimpliaDz.Api.Extensions;

public static class DependencyInjectionExtensions
{
    // Permissions used to register authorization policies.
    private static readonly string[] AllPermissions =
    [
        "users.view", "users.create", "users.edit", "users.delete",
        "roles.view", "roles.manage",
        "permissions.view", "permissions.manage",
        "products.view", "products.create", "products.edit", "products.delete",
    ];

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        var connectionString = configuration["Tenancy:Tenants:dev:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing Tenancy:Tenants:dev:ConnectionString in API appsettings.json");
        }

        var modules = ModuleRegistry.GetModules();

        foreach (var module in modules)
        {
            module.Register(services, connectionString);
        }

        var useCaseAssemblies = new[]
        {
            typeof(SimpliaDz.Inventory.UseCases.AssemblyMarker).Assembly,
            typeof(SimpliaDz.Identity.UseCases.AssemblyMarker).Assembly
        };

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(useCaseAssemblies));

        services.AddValidatorsFromAssemblies(useCaseAssemblies);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddAuthorization(options =>
        {
            foreach (var permission in AllPermissions)
            {
                options.AddPolicy(permission, policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireClaim("permission", permission));
            }
        });

        return services;
    }
}
