using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Simplia.Sales.Infrastructure.Data;

public sealed class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = Environment.GetEnvironmentVariable("SIMPLIA_DEV_CONNECTIONSTRING")
                 ?? config.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException(
                "No connection string found for design-time. " +
                "Set SIMPLIA_DEV_CONNECTIONSTRING or provide ConnectionStrings:Default in appsettings.json.");
        }

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(cs, sql =>
            {
                sql.MigrationsAssembly(typeof(SalesDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales");
            })
            .Options;

        return new SalesDbContext(options);
    }
}
