using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Accounting.Core;
using SimpliaDz.Accounting.Infrastructure.Data;
using SimpliaDz.Accounting.Infrastructure.Services;
using SimpliaDz.Accounting.UseCases.Services;
using SimpliaDz.BuildingBlocks.Exceptions;
using Xunit;

namespace SimpliaDz.Accounting.Tests;

public sealed class ExpenseIntegrationTests
{
    [Fact]
    public async Task Expenses_can_be_filtered_and_updated_while_retaining_historical_references()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzExpenseTest_{Guid.NewGuid():N}";
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
            var rent = new ExpenseCategory("Rent");
            var supplies = new ExpenseCategory("Supplies");
            db.ExpenseCategories.AddRange(rent, supplies);
            await db.SaveChangesAsync();
            var location = Guid.NewGuid();
            var service = new ExpenseService(db, new TestLocations(location));
            var actor = Guid.NewGuid();
            var first = await service.CreateAsync(rent.Id, null, 100m,
                new DateOnly(2026, 11, 1), "Monthly rent", actor, CancellationToken.None);
            var second = await service.CreateAsync(supplies.Id, location, 50.5m,
                new DateOnly(2026, 11, 2), "Paper", actor, CancellationToken.None);
            var totals = new ExpenseTotalReader(db);
            Assert.Equal(50.5m, await totals.GetTotalForDateAsync(
                new DateOnly(2026, 11, 2), CancellationToken.None));
            Assert.Equal(0m, await totals.GetTotalForDateAsync(
                new DateOnly(2026, 11, 4), CancellationToken.None));
            Assert.Null(first.LocationId);
            Assert.Equal(location, second.LocationId);
            Assert.Single(await service.ListAsync(new DateOnly(2026, 11, 2),
                new DateOnly(2026, 11, 2), null, null, 1, 50, CancellationToken.None));
            Assert.Single(await service.ListAsync(null, null, rent.Id,
                null, 1, 50, CancellationToken.None));
            Assert.Single(await service.ListAsync(null, null, null,
                location, 1, 50, CancellationToken.None));
            await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(rent.Id,
                Guid.NewGuid(), 10m, new DateOnly(2026, 11, 2), null, actor,
                CancellationToken.None));

            rent.Deactivate();
            await db.SaveChangesAsync();
            Assert.Equal(rent.Id, (await service.GetByIdAsync(first.Id,
                CancellationToken.None)).ExpenseCategoryId);
            await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(rent.Id,
                null, 10m, new DateOnly(2026, 11, 2), null, actor,
                CancellationToken.None));
            var updated = await service.UpdateAsync(first.Id, rent.Id, null,
                120m, new DateOnly(2026, 11, 3), "Adjusted rent", CancellationToken.None);
            Assert.Equal(actor, updated.CreatedBy);
            Assert.Equal(rent.Id, updated.ExpenseCategoryId);
            Assert.Equal(120m, updated.Amount);
            updated = await service.UpdateAsync(first.Id, supplies.Id, null,
                120m, new DateOnly(2026, 11, 3), "Reclassified", CancellationToken.None);
            Assert.Equal(supplies.Id, updated.ExpenseCategoryId);
            Assert.Equal(2, (await service.ListAsync(null, null, supplies.Id,
                null, 1, 50, CancellationToken.None)).Count);
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(first.Id,
                rent.Id, null, 120m, new DateOnly(2026, 11, 3), null,
                CancellationToken.None));
        }
        finally
        {
            await using var cleanup = NewDb(connection);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static AccountingDbContext NewDb(string connection) => new(
        new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "accounting"))
            .Options);

    private sealed class TestLocations(Guid activeLocationId) : IExpenseLocationValidator
    {
        public Task<bool> IsActiveLocationAsync(Guid locationId, CancellationToken ct) =>
            Task.FromResult(locationId == activeLocationId);
    }
}
