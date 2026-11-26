using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Simplia.Notifications.Infrastructure.Data;

public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlServer(cs, sql =>
            {
                sql.MigrationsAssembly(typeof(NotificationsDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications");
            })
            .Options;

        return new NotificationsDbContext(options);
    }
}
