using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SimpliaDz.Organization.Infrastructure.Data;

public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("SIMPLIADZ_DEV_CONNECTIONSTRING")
            ?? configuration.GetConnectionString("DesignTime")
            ?? configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No design-time connection string found. Set SIMPLIADZ_DEV_CONNECTIONSTRING " +
                "or provide ConnectionStrings:DesignTime.");
        }

        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(OrganizationDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "organization");
            })
            .Options;

        return new OrganizationDbContext(options);
    }
}
