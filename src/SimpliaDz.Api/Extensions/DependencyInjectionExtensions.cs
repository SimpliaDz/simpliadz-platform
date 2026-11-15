using FluentValidation;
using Mediator;

using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Mediation;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Resolution;
using SimpliaDz.SharedKernel.Authorization;
using SimpliaDz.Purchasing.UseCases.Services;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.Accounting.UseCases.Services;
using SimpliaDz.Reporting.UseCases.SalesSummary;
using SimpliaDz.Reporting.UseCases.ProductPerformance;
using SimpliaDz.Reporting.UseCases.Inventory;
using SimpliaDz.Reporting.UseCases.LowStock;
using SimpliaDz.Reporting.UseCases.Dashboard;

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
        services.AddScoped<IExpenseLocationValidator, ExpenseLocationValidator>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<ISaleReturnService, SaleReturnService>();
        services.AddScoped<IPosProductLookupService, PosProductLookupService>();
        services.AddScoped<ISalesSummaryReportService, SalesSummaryReportService>();
        services.AddScoped<IProductPerformanceReportService, ProductPerformanceReportService>();
        services.AddScoped<IInventoryReportService, InventoryReportService>();
        services.AddScoped<ILowStockReportService, LowStockReportService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDashboardSummaryService, DashboardSummaryService>();
        services.AddScoped<ICashRegisterService, CashRegisterService>();
        services.AddScoped<ICashSessionService, CashSessionService>();

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
