using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SimpliaDz.Reporting.Infrastructure.Data;

public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseSqlServer(cs, sql =>
            {
                sql.MigrationsAssembly(typeof(ReportingDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "reporting");
            })
            .Options;

        return new ReportingDbContext(options);
    }
}
