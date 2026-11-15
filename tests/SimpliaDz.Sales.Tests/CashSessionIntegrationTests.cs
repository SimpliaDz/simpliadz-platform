using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Api.Modules;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.Infrastructure.Services;
using Xunit;

namespace SimpliaDz.Sales.Tests;

public sealed class CashSessionIntegrationTests
{
    [Fact]
    public async Task Multiple_registers_and_cashiers_share_open_sessions_without_mixing_cash()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzCashTest_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(baseConnection)
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
            await using var inventory = new InventoryDbContext(
                new DbContextOptionsBuilder<InventoryDbContext>()
                    .UseSqlServer(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
                    .Options);
            await using var sales = new SalesDbContext(
                new DbContextOptionsBuilder<SalesDbContext>()
                    .UseSqlServer(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "sales"))
                    .Options);
            await inventory.Database.MigrateAsync();
            await sales.Database.MigrateAsync();
            var location = new InventoryLocation("Main shop");
            inventory.Locations.Add(location);
            await inventory.SaveChangesAsync();

            var registers = new CashRegisterService(sales, inventory);
            var first = await registers.CreateAsync(location.Id, "R1", "Front", CancellationToken.None);
            var second = await registers.CreateAsync(location.Id, "R2", "Back", CancellationToken.None);
            var sessions = new CashSessionService(sales, inventory);
            var opener = Guid.NewGuid();
            var firstSession = await sessions.OpenAsync(first.Id, 100m, opener, CancellationToken.None);
            var secondSession = await sessions.OpenAsync(second.Id, 20m, opener, CancellationToken.None);
            Assert.Equal(2, (await sessions.GetCurrentAsync(location.Id, CancellationToken.None)).Count);
            await Assert.ThrowsAsync<ConflictException>(() => sessions.OpenAsync(first.Id, 0m,
                Guid.NewGuid(), CancellationToken.None));

            var cashier1 = Guid.NewGuid();
            var cashier2 = Guid.NewGuid();
            var sale = new Sale(location.Id, null, cashier1, Guid.NewGuid());
            sale.AddLine(Guid.NewGuid(), 1, 30m, 10m);
            sale.Complete(cashier1);
            sales.Sales.Add(sale);
            await sales.SaveChangesAsync();
            var payments = new PaymentService(sales);
            await Assert.ThrowsAsync<ConflictException>(() => payments.RecordAsync(Guid.NewGuid(),
                sale.Id, 10m, PaymentMethod.Cash, cashier1, CancellationToken.None));
            await using (var cashier1Db = new SalesDbContext(
                new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(connectionString).Options))
            await using (var cashier2Db = new SalesDbContext(
                new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(connectionString).Options))
            {
                await Task.WhenAll(
                    new PaymentService(cashier1Db).RecordAsync(Guid.NewGuid(), sale.Id, 10m,
                        PaymentMethod.Cash, cashier1, CancellationToken.None, firstSession.Id),
                    new PaymentService(cashier2Db).RecordAsync(Guid.NewGuid(), sale.Id, 15m,
                        PaymentMethod.Cash, cashier2, CancellationToken.None, firstSession.Id));
            }
            sales.ChangeTracker.Clear();
            await payments.RecordAsync(Guid.NewGuid(), sale.Id, 5m, PaymentMethod.Card,
                cashier2, CancellationToken.None, firstSession.Id);
            await sessions.AddMovementAsync(firstSession.Id, -5m, "Petty cash",
                cashier1, CancellationToken.None);
            var closed = await sessions.CloseAsync(firstSession.Id, 119m, cashier2,
                CancellationToken.None);
            Assert.Equal(120m, closed.ExpectedCash);
            Assert.Equal(-1m, closed.Difference);
            Assert.Equal(25m, closed.CashPayments);
            Assert.Equal(5m, closed.OtherPayments);
            Assert.Equal(20m, (await sessions.GetAsync(secondSession.Id,
                CancellationToken.None)).ExpectedCash);
            await Assert.ThrowsAsync<ConflictException>(() => sessions.CloseAsync(firstSession.Id,
                119m, cashier2, CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() => payments.RecordAsync(Guid.NewGuid(),
                sale.Id, 1m, PaymentMethod.Cash, cashier1, CancellationToken.None, firstSession.Id));
            await registers.DeactivateAsync(first.Id, CancellationToken.None);
            await Assert.ThrowsAsync<ConflictException>(() => registers.DeactivateAsync(second.Id,
                CancellationToken.None));
        }
        finally
        {
            await using var cleanup = new SalesDbContext(
                new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(connectionString).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
