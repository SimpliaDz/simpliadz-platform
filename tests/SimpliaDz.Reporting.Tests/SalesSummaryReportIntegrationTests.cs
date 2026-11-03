using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Reporting.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using Xunit;

namespace SimpliaDz.Reporting.Tests;

public sealed class SalesSummaryReportIntegrationTests
{
    [Fact]
    public async Task Report_uses_completed_sale_and_cost_snapshots_with_inclusive_utc_dates()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzReportTest_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnection)
            { InitialCatalog = name }.ConnectionString;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(baseConnection)
                     { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{name}]", master);
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await using var db = NewDb(connection);
            await db.Database.MigrateAsync();
            await using (var reporting = new ReportingDbContext(
                new DbContextOptionsBuilder<ReportingDbContext>()
                    .UseSqlServer(connection).Options))
                await reporting.Database.MigrateAsync();
            var actor = Guid.NewGuid();
            var location = Guid.NewGuid();
            var prices = Guid.NewGuid();
            var first = new Sale(location, null, actor, prices);
            first.AddLine(Guid.NewGuid(), 2, 20m, 8m, 2m);
            first.ApplySaleDiscount(SaleDiscountType.FixedAmount, 3m);
            first.Complete(actor);
            var returned = new Sale(location, null, actor, prices);
            var returnedLine = returned.AddLine(Guid.NewGuid(), 1, 10m, 4m);
            returned.Complete(actor);
            returned.RecordReturn(returnedLine.Id, 1);
            var earlier = new Sale(location, null, actor, prices);
            earlier.AddLine(Guid.NewGuid(), 1, 100m, 50m);
            earlier.Complete(actor);
            var draft = new Sale(location, null, actor, prices);
            draft.AddLine(Guid.NewGuid(), 1, 1000m, 500m);
            db.Sales.AddRange(first, returned, earlier, draft);
            await db.SaveChangesAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);
            var earlierAt = new DateTimeOffset(yesterday.ToDateTime(new TimeOnly(12, 0),
                DateTimeKind.Utc));
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [sales].[Sales] SET [SaleDate] = {earlierAt}
                WHERE [Id] = {earlier.Id}
                """);

            var report = new SalesSummaryReportService(db, new TestTenant(connection));
            var summary = await report.GetAsync(today, today, CancellationToken.None);
            Assert.Equal(45m, summary.Revenue);
            Assert.Equal(20m, summary.CostOfGoodsSold);
            Assert.Equal(25m, summary.GrossProfit);
            Assert.Equal(2, summary.TransactionCount);
            Assert.Equal(3, summary.ItemsSold);
            Assert.Equal(22.5m, summary.AverageTransactionValue);

            var previous = await report.GetAsync(yesterday, yesterday,
                CancellationToken.None);
            Assert.Equal(100m, previous.Revenue);
            Assert.Equal(50m, previous.CostOfGoodsSold);
            Assert.Equal(1, previous.TransactionCount);
            var empty = await report.GetAsync(today.AddDays(1), today.AddDays(1),
                CancellationToken.None);
            Assert.Equal(0m, empty.Revenue);
            Assert.Equal(0m, empty.AverageTransactionValue);
            Assert.Equal(0, empty.TransactionCount);
            await Assert.ThrowsAsync<ArgumentException>(() => report.GetAsync(today,
                yesterday, CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() =>
                new SalesSummaryReportService(db, new TestTenant(connection, false))
                    .GetAsync(today, today, CancellationToken.None));
        }
        finally
        {
            await using var cleanup = NewDb(connection);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static SalesDbContext NewDb(string connection) => new(
        new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales"))
            .Options);

    private sealed class TestTenant(string connection, bool salesEnabled = true) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) =>
            moduleName == "Reporting" || (salesEnabled && moduleName == "Sales");
    }
}
