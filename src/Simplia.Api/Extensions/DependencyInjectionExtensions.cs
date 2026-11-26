using FluentValidation;
using Mediator;

using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Mediation;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Resolution;
using Simplia.SharedKernel.Authorization;
using Simplia.Purchasing.UseCases.Services;
using Simplia.Sales.UseCases.Services;
using Simplia.Accounting.UseCases.Services;
using Simplia.Reporting.UseCases.SalesSummary;
using Simplia.Reporting.UseCases.ProductPerformance;
using Simplia.Reporting.UseCases.Inventory;
using Simplia.Reporting.UseCases.LowStock;
using Simplia.Reporting.UseCases.Dashboard;
using Simplia.Reporting.UseCases.FinancialDashboard;

namespace Simplia.Api.Extensions;

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
        services.AddScoped<SalesPdfDocumentService>();
        services.AddScoped<ISaleReturnService, SaleReturnService>();
        services.AddScoped<IPosProductLookupService, PosProductLookupService>();
        services.AddScoped<ISalesSummaryReportService, SalesSummaryReportService>();
        services.AddScoped<IFinancialDashboardService, FinancialDashboardService>();
        services.AddScoped<IProductPerformanceReportService, ProductPerformanceReportService>();
        services.AddScoped<IInventoryReportService, InventoryReportService>();
        services.AddScoped<ILowStockReportService, LowStockReportService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDashboardSummaryService, DashboardSummaryService>();
        services.AddScoped<ICashRegisterService, CashRegisterService>();
        services.AddScoped<ICashSessionService, CashSessionService>();
        services.AddScoped<IPosService, PosService>();
        services.AddScoped<InventoryWarningScanService>();
        services.AddHostedService<InventoryWarningWorker>();
        services.AddScoped<WorkSessionService>();

        var useCaseAssemblies = new[]
        {
            typeof(Simplia.Inventory.UseCases.AssemblyMarker).Assembly,
            typeof(Simplia.Identity.UseCases.AssemblyMarker).Assembly,
            typeof(Simplia.Organization.UseCases.AssemblyMarker).Assembly
        };

        services.AddMediator(options =>
        {
            // Handlers depend on request-scoped repositories and DbContexts.
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies =
            [
                typeof(Simplia.Inventory.UseCases.AssemblyMarker),
                typeof(Simplia.Identity.UseCases.AssemblyMarker),
                typeof(Simplia.Organization.UseCases.AssemblyMarker)
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
