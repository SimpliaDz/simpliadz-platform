using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy.Resolution;
using SimpliaDz.ModuleCatalog;

var hostBuilder = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: false);
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
