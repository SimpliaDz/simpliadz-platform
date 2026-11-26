using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Crm.Infrastructure.Data;
using Simplia.Crm.Infrastructure.Services;
using Simplia.Sales.Contracts.Loyalty;
using Xunit;

namespace Simplia.Crm.Tests;

public sealed class LoyaltyIntegrationTests
{
    [Fact]
    public async Task Sale_and_return_sync_is_idempotent_and_rewards_debit_the_card_once()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var name = $"SimpliaCrmTest_{Guid.NewGuid():N}";
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
            Assert.Null(await service.GetProgramAsync(CancellationToken.None));
            await service.ConfigureProgramAsync("Simplia Points", 100m, true, CancellationToken.None);
            Assert.Equal("Simplia Points", (await service.GetProgramAsync(CancellationToken.None))!.Name);
            var card = await service.EnrollAsync(customerId, CancellationToken.None);
            Assert.StartsWith("LDZ", card.CardNumber);
            await Assert.ThrowsAsync<ConflictException>(() => service.EnrollAsync(customerId,
                CancellationToken.None));
            Assert.Equal(card.Id, (await service.GetCardByIdAsync(card.Id,
                CancellationToken.None)).Id);
            Assert.Equal(card.Id, (await service.GetCardByNumberAsync(card.CardNumber,
                CancellationToken.None)).Id);
            Assert.False((await service.SetCardActiveAsync(card.Id, false,
                CancellationToken.None)).IsActive);
            Assert.True((await service.SetCardActiveAsync(card.Id, true,
                CancellationToken.None)).IsActive);

            var inactiveReward = await service.CreateRewardAsync("Old gift", 3,
                CancellationToken.None);
            inactiveReward = await service.UpdateRewardAsync(inactiveReward.Id,
                "Updated gift", 4, false, CancellationToken.None);
            Assert.False((await service.GetRewardByIdAsync(inactiveReward.Id,
                CancellationToken.None)).IsActive);
            Assert.DoesNotContain(await service.ListRewardsAsync(false, CancellationToken.None),
                reward => reward.Id == inactiveReward.Id);
            Assert.Contains(await service.ListRewardsAsync(true, CancellationToken.None),
                reward => reward.Id == inactiveReward.Id);

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
            Assert.Equal(4, (await service.GetHistoryAsync(card.Id, 1, 20,
                CancellationToken.None)).Count);

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
