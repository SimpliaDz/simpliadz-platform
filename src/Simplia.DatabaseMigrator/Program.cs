using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.BuildingBlocks.Tenancy.Resolution;
using Simplia.ModuleCatalog;

var hostBuilder = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true);
        var tenantCatalogPath = Environment.GetEnvironmentVariable("SIMPLIA_TENANTS_FILE");
        if (!string.IsNullOrWhiteSpace(tenantCatalogPath))
            cfg.AddJsonFile(Path.GetFullPath(tenantCatalogPath), optional: false,
                reloadOnChange: false);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddTenancyFromConfiguration(ctx.Configuration);
        services.AddSingleton<ITenantResolver>(new CliTenantResolver(args));

        foreach (var installer in ModuleCatalog.GetModules())
        {
            services.AddSingleton<IModuleInstaller>(installer);
        }

        using var tempProvider = services.BuildServiceProvider();
        var tenant = tempProvider.GetRequiredService<ITenantContext>();

        Console.WriteLine($"Tenant: {tenant.TenantId} ({tenant.Name})");

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

using var scope = host.Services.CreateScope();
var serviceProvider = scope.ServiceProvider;
var tenant = serviceProvider.GetRequiredService<ITenantContext>();

foreach (var installer in serviceProvider.GetRequiredService<IEnumerable<IModuleInstaller>>())
{
    if (!tenant.IsModuleEnabled(installer.ModuleName))
    {
        Console.WriteLine($"- Skipping module '{installer.ModuleName}'");
        continue;
    }

    Console.WriteLine($"- Migrating '{installer.ModuleName}'");
    await installer.MigrateAsync(serviceProvider, CancellationToken.None);

    Console.WriteLine($"- Seeding '{installer.ModuleName}'");
    await installer.SeedAsync(serviceProvider, CancellationToken.None);
}

Console.WriteLine("Migration completed.");

if (args.Any(arg => string.Equals(arg, "--seed-demo", StringComparison.OrdinalIgnoreCase)))
{
    await DemoDataSeeder.SeedAsync(serviceProvider, tenant, CancellationToken.None);
    Console.WriteLine("Demo seed completed.");
}
