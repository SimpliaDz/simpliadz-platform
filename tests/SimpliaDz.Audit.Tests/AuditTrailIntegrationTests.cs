using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Audit.Infrastructure;
using SimpliaDz.Audit.Infrastructure.Data;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using Xunit;

namespace SimpliaDz.Audit.Tests;

public sealed class AuditTrailIntegrationTests
{
    [Fact]
    public async Task Entries_are_append_only_queryable_and_reject_authentication_fields()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzAuditTest_{Guid.NewGuid():N}";
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
            var audit = new AuditTrail(db, new TestTenant(connection));
            var actor = Guid.NewGuid();
            var variant = Guid.NewGuid();
            await audit.AppendAsync(actor, "StockAdjusted", "ProductVariant", variant,
                new Dictionary<string, object?> { ["quantityOnHand"] = 7 },
                new Dictionary<string, object?> { ["quantityOnHand"] = 5 },
                "request-123", CancellationToken.None);
            await audit.AppendAsync(actor, "PriceChanged", "ProductVariant", variant,
                new Dictionary<string, object?> { ["price"] = 100m },
                new Dictionary<string, object?> { ["price"] = 120m,
                    ["paymentDate"] = new DateOnly(2026, 11, 25),
                    ["paidAt"] = new DateTimeOffset(2026, 11, 25, 17, 30, 0, TimeSpan.FromHours(-5)) },
                "request-456", CancellationToken.None);
            await Assert.ThrowsAsync<ArgumentException>(() => audit.AppendAsync(actor,
                "UserDeactivated", "User", Guid.NewGuid(), null,
                new Dictionary<string, object?> { ["passwordHash"] = "secret" },
                null, CancellationToken.None));

            await Assert.ThrowsAsync<ArgumentException>(() => audit.AppendAsync(actor,
                "InvalidAudit", "ProductVariant", variant, null,
                new Dictionary<string, object?> { ["values"] = new[] { 1, 2 } },
                null, CancellationToken.None));
            var paymentHistory = await audit.QueryAsync(actor, "PriceChanged", "ProductVariant",
                variant, null, null, 1, 50, CancellationToken.None);
            using var paymentValues = System.Text.Json.JsonDocument.Parse(Assert.Single(paymentHistory.Items).NewValues!);
            Assert.Equal(new DateOnly(2026, 11, 25),
                paymentValues.RootElement.GetProperty("paymentDate").Deserialize<DateOnly>());
            Assert.Equal("2026-11-25T17:30:00-05:00", paymentValues.RootElement.GetProperty("paidAt").GetString());

            var history = await audit.QueryAsync(actor, "StockAdjusted", "ProductVariant",
                variant, null, null, 1, 50, CancellationToken.None);
            Assert.Equal(1, history.TotalItems);
            var entry = Assert.Single(history.Items);
            Assert.Equal(actor, entry.UserId);
            Assert.Equal(variant, entry.EntityId);
            Assert.Equal("request-123", entry.CorrelationId);
            Assert.Contains("\"quantityOnHand\":7", entry.OldValues);
            Assert.Contains("\"quantityOnHand\":5", entry.NewValues);
            Assert.InRange(entry.Timestamp, DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(1));
            Assert.Equal(2, (await audit.QueryAsync(null, null, null, null,
                null, null, 2, 1, CancellationToken.None)).TotalItems);

            var tracked = await db.Entries.FirstAsync();
            db.Entry(tracked).Property(x => x.Action).CurrentValue = "Tampered";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Entries.Remove(await db.Entries.FirstAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = NewDb(connection);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static AuditDbContext NewDb(string connection) => new(
        new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "audit"))
            .Options);

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "test";
        public string Name => "test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName == "Audit";
    }
}
