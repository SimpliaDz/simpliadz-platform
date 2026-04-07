using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SimpliaDz.Audit.Infrastructure.Data;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = Environment.GetEnvironmentVariable("SIMPLIADZ_DEV_CONNECTIONSTRING")
                 ?? config.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException(
                "No connection string found for design-time. " +
                "Set SIMPLIADZ_DEV_CONNECTIONSTRING or provide ConnectionStrings:Default in appsettings.json.");
        }

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(cs, sql =>
            {
                sql.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "audit");
            })
            .Options;

        return new AuditDbContext(options);
    }
}
