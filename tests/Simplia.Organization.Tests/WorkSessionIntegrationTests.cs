using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Organization.Core.Entities;
using Simplia.Organization.Infrastructure.Data;
using OrganizationEntity = Simplia.Organization.Core.Entities.Organization;
using Xunit;

namespace Simplia.Organization.Tests;

public sealed class WorkSessionIntegrationTests
{
    [Fact]
    public async Task Overnight_hours_split_by_local_day_and_manager_corrections_are_preserved()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaWorkTest_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnection)
            { InitialCatalog = name }.ConnectionString;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(baseConnection)
                     { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var create = new SqlCommand($"CREATE DATABASE [{name}]", master);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            await using var db = NewDb(connection);
            await db.Database.MigrateAsync();
            db.Organizations.Add(new OrganizationEntity("Work test", null, null, null, null,
                "DZD", null, true, "Africa/Algiers"));
            await db.SaveChangesAsync();
            var clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 14, 21, 30, 0,
                TimeSpan.Zero));
            var service = new WorkSessionService(db, clock);
            var employee = Guid.NewGuid();
            var manager = Guid.NewGuid();
            var session = await service.ClockInAsync(employee, CancellationToken.None);
            await Assert.ThrowsAsync<ConflictException>(() => service.ClockInAsync(employee,
                CancellationToken.None));
            clock.Now = new DateTimeOffset(2026, 11, 15, 1, 30, 0, TimeSpan.Zero);
            var closed = await service.ClockOutAsync(session.Id, employee, CancellationToken.None);
            Assert.NotNull(closed.ClockOutAt);
            await Assert.ThrowsAsync<ConflictException>(() => service.ClockOutAsync(session.Id,
                employee, CancellationToken.None));
            var before = await service.ReportAsync(employee, new DateOnly(2026, 11, 14),
                new DateOnly(2026, 11, 15), CancellationToken.None);
            Assert.Equal("Africa/Algiers", before.TimeZoneId);
            Assert.Equal(4m, before.TotalHours);
            Assert.Equal(1.5m, before.Days.Single(x => x.Date == new DateOnly(2026, 11, 14)).Hours);
            Assert.Equal(2.5m, before.Days.Single(x => x.Date == new DateOnly(2026, 11, 15)).Hours);

            await service.CorrectAsync(session.Id,
                new DateTimeOffset(2026, 11, 14, 21, 0, 0, TimeSpan.Zero),
                closed.ClockOutAt, "Correct clock-in", manager, CancellationToken.None);
            var after = await service.ReportAsync(employee, new DateOnly(2026, 11, 14),
                new DateOnly(2026, 11, 15), CancellationToken.None);
            Assert.Equal(4.5m, after.TotalHours);
            Assert.Single(await service.CorrectionsAsync(session.Id, CancellationToken.None));
            clock.Now = new DateTimeOffset(2026, 11, 15, 1, 45, 0, TimeSpan.Zero);
            await service.ClockInAsync(employee, CancellationToken.None);
            await Assert.ThrowsAsync<ConflictException>(() => service.CorrectAsync(session.Id,
                new DateTimeOffset(2026, 11, 14, 21, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 15, 2, 0, 0, TimeSpan.Zero),
                "Would overlap next shift", manager, CancellationToken.None));
            var correction = await db.WorkSessionCorrections.SingleAsync();
            db.WorkSessionCorrections.Remove(correction);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = NewDb(connection);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static OrganizationDbContext NewDb(string connection) => new(
        new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "organization"))
            .Options);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
