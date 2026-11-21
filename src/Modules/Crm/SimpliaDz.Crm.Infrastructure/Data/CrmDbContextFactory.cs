using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SimpliaDz.Crm.Infrastructure.Data;

public sealed class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("SIMPLIADZ_DEV_CONNECTIONSTRING")
            ?? throw new InvalidOperationException("Set SIMPLIADZ_DEV_CONNECTIONSTRING for CRM migrations.");
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(connection, sql =>
            {
                sql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "crm");
            }).Options;
        return new CrmDbContext(options);
    }
}
