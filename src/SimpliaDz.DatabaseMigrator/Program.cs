using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Resolution;
using SimpliaDz.Inventory.Infrastructure;
using SimpliaDz.Identity.Infrastructure;
using SimpliaDz.Sales.Infrastructure;
using SimpliaDz.Purchasing.Infrastructure;
using SimpliaDz.Accounting.Infrastructure;
using SimpliaDz.Notifications.Infrastructure;
using SimpliaDz.Reporting.Infrastructure;
using SimpliaDz.Audit.Infrastructure;


var hostBuilder = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: false);
    })
    .ConfigureServices((ctx, services) =>
    {
        // ---- Tenancy (Option 2)
        services.AddTenancyFromConfiguration(ctx.Configuration);

        // ---- CLI tenant resolver
        services.AddSingleton<ITenantResolver>(new CliTenantResolver(args));

        // ---- Register module installers (explicit)
        services.AddSingleton<IModuleInstaller, InventoryModuleInstaller>();
        services.AddSingleton<IModuleInstaller, IdentityModuleInstaller>();
        services.AddSingleton<IModuleInstaller, SalesModuleInstaller>();
        services.AddSingleton<IModuleInstaller, PurchasingModuleInstaller>();
        services.AddSingleton<IModuleInstaller, AccountingModuleInstaller>();
        services.AddSingleton<IModuleInstaller, NotificationsModuleInstaller>();
        services.AddSingleton<IModuleInstaller, ReportingModuleInstaller>();
        services.AddSingleton<IModuleInstaller, AuditModuleInstaller>();


        // ---- Resolve tenant early (TEMP provider, SAFE)
        using var tempProvider = services.BuildServiceProvider();
        var tenant = tempProvider.GetRequiredService<ITenantContext>();

        Console.WriteLine($"Tenant: {tenant.TenantId} ({tenant.Name})");

        // ---- Register ONLY enabled modules
        foreach (var installer in tempProvider.GetRequiredService<IEnumerable<IModuleInstaller>>())
        {
            if (!tenant.IsModuleEnabled(installer.ModuleName))
            {
                Console.WriteLine($"- Skipping module '{installer.ModuleName}'");
                continue;
            }

            Console.WriteLine($"- Registering module '{installer.ModuleName}'");
            installer.Register(services, tenant.ConnectionString);
        }
    });

var host = hostBuilder.Build();

// ---- Migrate + Seed
using var scope = host.Services.CreateScope();
var serviceProvider = scope.ServiceProvider;

foreach (var installer in serviceProvider.GetRequiredService<IEnumerable<IModuleInstaller>>())
{
    Console.WriteLine($"- Migrating '{installer.ModuleName}'");
    await installer.MigrateAsync(serviceProvider, CancellationToken.None);

    Console.WriteLine($"- Seeding '{installer.ModuleName}'");
    await installer.SeedAsync(serviceProvider, CancellationToken.None);
}

Console.WriteLine("Migration completed.");