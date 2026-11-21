using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Crm.Infrastructure.Data;
using SimpliaDz.Crm.Infrastructure.Services;
using SimpliaDz.Sales.Contracts.Loyalty;
using Xunit;

namespace SimpliaDz.Crm.Tests;

public sealed class LoyaltyIntegrationTests
{
    [Fact]
    public async Task Sale_and_return_sync_is_idempotent_and_rewards_debit_the_card_once()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIADZ_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaDzCrmTest_{Guid.NewGuid():N}";
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
            var options = new DbContextOptionsBuilder<CrmDbContext>()
                .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "crm"))
                .Options;
            await using var db = new CrmDbContext(options);
            await db.Database.MigrateAsync();
            var customerId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var saleId = Guid.NewGuid();
            var firstReturnId = Guid.NewGuid();
            var secondReturnId = Guid.NewGuid();
            var sales = new FakeSalesReader(customerId);
            var service = new LoyaltyService(db, sales);
            await service.ConfigureProgramAsync("Simplia Points", 100m, true, CancellationToken.None);
            var card = await service.EnrollAsync(customerId, CancellationToken.None);
            Assert.StartsWith("LDZ", card.CardNumber);
            Assert.Equal(card.Id, (await service.GetCardByNumberAsync(card.CardNumber,
                CancellationToken.None)).Id);

            sales.Sale = new LoyaltySaleSnapshot(saleId, customerId, actorId,
                "Completed", DateTimeOffset.UtcNow.AddSeconds(1), 250m, []);
            Assert.Equal(2, (await service.SyncSaleAsync(saleId, CancellationToken.None))!.PointsBalance);
            Assert.Equal(2, (await service.SyncSaleAsync(saleId, CancellationToken.None))!.PointsBalance);

            sales.Sale = sales.Sale with { Status = "PartiallyReturned", Returns =
                [new LoyaltyReturnSnapshot(firstReturnId, 150m, actorId)] };
            Assert.Equal(1, (await service.SyncSaleAsync(saleId, CancellationToken.None))!.PointsBalance);
            Assert.Equal(1, (await service.SyncSaleAsync(saleId, CancellationToken.None))!.PointsBalance);

            var reward = await service.CreateRewardAsync("Small gift", 1, CancellationToken.None);
            var redemptionId = Guid.NewGuid();
            var redeemed = await service.RedeemAsync(card.Id, reward.Id, redemptionId,
                actorId, CancellationToken.None);
            Assert.Equal(-1, redeemed.PointsDelta);
            Assert.Equal(redeemed.Id, (await service.RedeemAsync(card.Id, reward.Id,
                redemptionId, actorId, CancellationToken.None)).Id);
            Assert.Equal(0, (await service.GetCardByCustomerAsync(customerId,
                CancellationToken.None)).PointsBalance);

            sales.Sale = sales.Sale with { Status = "Returned", Returns =
                [new LoyaltyReturnSnapshot(firstReturnId, 150m, actorId),
                 new LoyaltyReturnSnapshot(secondReturnId, 100m, actorId)] };
            Assert.Equal(-1, (await service.SyncSaleAsync(saleId, CancellationToken.None))!.PointsBalance);
            await Assert.ThrowsAsync<ConflictException>(() => service.RedeemAsync(
                card.Id, reward.Id, Guid.NewGuid(), actorId, CancellationToken.None));
            Assert.Equal(4, await db.Transactions.CountAsync());

            var stored = await db.Transactions.FirstAsync();
            db.Transactions.Remove(stored);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = new CrmDbContext(new DbContextOptionsBuilder<CrmDbContext>()
                .UseSqlServer(connection).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class FakeSalesReader(Guid customerId) : ILoyaltySalesReader
    {
        public LoyaltySaleSnapshot? Sale { get; set; }
        public Task<bool?> IsCustomerActiveAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<bool?>(id == customerId ? true : null);
        public Task<LoyaltySaleSnapshot?> GetSaleAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Sale?.Id == id ? Sale : null);
        public Task<IReadOnlyList<Guid>> GetCompletedSaleIdsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(id == customerId && Sale is not null
                ? [Sale.Id] : []);
    }
}
