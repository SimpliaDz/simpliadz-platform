using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SimpliaDz.Identity.Infrastructure.Data;

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
        // SIMPLIADZ_DEV_CONNECTIONSTRING="Server=...;Database=...;"
        var cs = Environment.GetEnvironmentVariable("SIMPLIADZ_DEV_CONNECTIONSTRING")
                 ?? config.GetConnectionString("DesignTime");

        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException(
                "No connection string found for design-time. " +
                "Set SIMPLIADZ_DEV_CONNECTIONSTRING or provide ConnectionStrings:Default in appsettings.json.");
        }

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;

        return new IdentityDbContext(options);
    }
}
