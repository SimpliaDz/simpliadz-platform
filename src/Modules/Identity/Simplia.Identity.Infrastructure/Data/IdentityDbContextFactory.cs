using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Simplia.Identity.Infrastructure.Data;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        // This loads configuration for design-time only (dotnet ef).
        // We keep it simple: read connection string from env var first,
        // otherwise fallback to a local appsettings.Development.json (optional).
        var basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Prefer environment variable for EF tooling:
        // SIMPLIA_DEV_CONNECTIONSTRING="Server=...;Database=...;"
        var cs = Environment.GetEnvironmentVariable("SIMPLIA_DEV_CONNECTIONSTRING")
                 ?? config.GetConnectionString("DesignTime");

        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException(
                "No connection string found for design-time. " +
                "Set SIMPLIA_DEV_CONNECTIONSTRING or provide ConnectionStrings:Default in appsettings.json.");
        }

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;

        return new IdentityDbContext(options);
    }
}
