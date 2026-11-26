using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Crm.Contracts;
using Simplia.Crm.Core.Entities;
using Simplia.Crm.Infrastructure.Data;
using Simplia.Crm.UseCases.Loyalty;
using Simplia.Sales.Contracts.Loyalty;

namespace Simplia.Crm.Infrastructure.Services;

public sealed class LoyaltyService(CrmDbContext db, ILoyaltySalesReader sales) : ILoyaltyService
{
    public async Task<LoyaltyProgramDto?> GetProgramAsync(CancellationToken ct)
    {
        var program = await db.Programs.AsNoTracking().SingleOrDefaultAsync(ct);
        return program is null ? null : Map(program);
    }

    public async Task<LoyaltyProgramDto> ConfigureProgramAsync(string name,
        decimal spendAmountPerPoint, bool isActive, CancellationToken ct)
    {
        var program = await db.Programs.SingleOrDefaultAsync(ct);
        if (program is null)
        {
            program = new LoyaltyProgram(name, spendAmountPerPoint);
            db.Programs.Add(program);
        }
        else program.Update(name, spendAmountPerPoint);
        program.SetActive(isActive);
        await SaveAsync(ct);
        return Map(program);
    }

    public async Task<LoyaltyCardDto> EnrollAsync(Guid customerId, CancellationToken ct)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID is required.", nameof(customerId));
        var program = await db.Programs.AsNoTracking().SingleOrDefaultAsync(ct);
        if (program is null || !program.IsActive)
            throw new ConflictException("Configure and activate the loyalty program first.");
        var active = await sales.IsCustomerActiveAsync(customerId, ct);
        if (active is null) throw new NotFoundException("Customer", customerId);
        if (!active.Value) throw new ConflictException("Inactive customers cannot be enrolled.");
        if (await db.Cards.AnyAsync(x => x.CustomerId == customerId, ct))
            throw new ConflictException("Customer already has a loyalty card.");
        var card = new LoyaltyCard(customerId);
        db.Cards.Add(card);
        await SaveAsync(ct);
        return Map(card);
    }

    public async Task<LoyaltyCardDto> GetCardByCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var card = await db.Cards.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customerId, ct)
            ?? throw new NotFoundException("Loyalty card for customer", customerId);
        var saleIds = await sales.GetCompletedSaleIdsAsync(customerId, ct);
        foreach (var saleId in saleIds) await SyncSaleAsync(saleId, ct);
        return Map(await LoadCardAsync(card.Id, ct));
    }

    public async Task<LoyaltyCardDto> GetCardByNumberAsync(string cardNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Trim().Length > 35)
            throw new ArgumentException("Card number is required.", nameof(cardNumber));
        var card = await db.Cards.AsNoTracking().SingleOrDefaultAsync(
            x => x.CardNumber == cardNumber.Trim().ToUpperInvariant(), ct)
            ?? throw new NotFoundException("Loyalty card", cardNumber.Trim());
        return await GetCardByCustomerAsync(card.CustomerId, ct);
    }

    public async Task<LoyaltyCardDto> GetCardByIdAsync(Guid cardId, CancellationToken ct)
    {
        var card = await LoadCardAsync(cardId, ct);
        return await GetCardByCustomerAsync(card.CustomerId, ct);
    }

    public async Task<LoyaltyCardDto> SetCardActiveAsync(Guid cardId, bool active, CancellationToken ct)
    {
        var card = await LoadCardAsync(cardId, ct);
        card.SetActive(active);
        await SaveAsync(ct);
        return Map(card);
    }

    public async Task<IReadOnlyList<LoyaltyTransactionDto>> GetHistoryAsync(
        Guid cardId, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        var card = await LoadCardAsync(cardId, ct);
        await GetCardByCustomerAsync(card.CustomerId, ct);
        var rows = await db.Transactions.AsNoTracking().Where(x => x.CardId == cardId)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return rows.Select(Map).ToArray();
    }

    public async Task<LoyaltyRewardDto> CreateRewardAsync(string name, int pointsCost,
        CancellationToken ct)
    {
        var reward = new LoyaltyReward(name, pointsCost);
        db.Rewards.Add(reward);
        await SaveAsync(ct);
        return Map(reward);
    }

    public async Task<LoyaltyRewardDto> GetRewardByIdAsync(Guid id, CancellationToken ct) =>
        Map(await db.Rewards.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Loyalty reward", id));

    public async Task<LoyaltyRewardDto> UpdateRewardAsync(Guid id, string name,
        int pointsCost, bool isActive, CancellationToken ct)
    {
        var reward = await db.Rewards.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Loyalty reward", id);
        reward.Update(name, pointsCost);
        reward.SetActive(isActive);
        await SaveAsync(ct);
        return Map(reward);
    }

    public async Task<IReadOnlyList<LoyaltyRewardDto>> ListRewardsAsync(
        bool includeInactive, CancellationToken ct)
    {
        var query = db.Rewards.AsNoTracking();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        return (await query.OrderBy(x => x.PointsCost).ThenBy(x => x.Name)
            .ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<LoyaltyTransactionDto> RedeemAsync(Guid cardId, Guid rewardId,
        Guid redemptionId, Guid actorId, CancellationToken ct)
    {
        if (cardId == Guid.Empty || rewardId == Guid.Empty || redemptionId == Guid.Empty ||
            actorId == Guid.Empty) throw new ArgumentException("Card, reward, redemption, and actor are required.");
        var previous = await db.Transactions.AsNoTracking().SingleOrDefaultAsync(
            x => x.SourceType == "Reward" && x.SourceId == redemptionId, ct);
        if (previous is not null)
        {
            if (previous.CardId != cardId || previous.RewardId != rewardId)
                throw new ConflictException("Redemption ID was already used for another reward.");
            return Map(previous);
        }
        var enrolledCard = await LoadCardAsync(cardId, ct);
        await GetCardByCustomerAsync(enrolledCard.CustomerId, ct);
        var program = await db.Programs.AsNoTracking().SingleOrDefaultAsync(ct);
        if (program is null || !program.IsActive)
            throw new ConflictException("Loyalty program is inactive.");
        var card = await LoadCardAsync(cardId, ct);
        if (!card.IsActive) throw new ConflictException("Loyalty card is inactive.");
        if (await sales.IsCustomerActiveAsync(card.CustomerId, ct) != true)
            throw new ConflictException("Customer is inactive.");
        var reward = await db.Rewards.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rewardId, ct)
            ?? throw new NotFoundException("Loyalty reward", rewardId);
        if (!reward.IsActive) throw new ConflictException("Reward is inactive.");
        if (card.PointsBalance < reward.PointsCost)
            throw new ConflictException("Not enough loyalty points.");
        var entry = new LoyaltyTransaction(card.Id, LoyaltyTransactionType.RewardRedeemed,
            -reward.PointsCost, "Reward", redemptionId, actorId,
            rewardNameSnapshot: reward.Name, rewardId: reward.Id);
        card.ApplyPoints(entry.PointsDelta);
        db.Transactions.Add(entry);
        await SaveAsync(ct);
        return Map(entry);
    }

    public async Task<LoyaltyCardDto?> SyncSaleAsync(Guid saleId, CancellationToken ct)
    {
        var sale = await sales.GetSaleAsync(saleId, ct)
            ?? throw new NotFoundException("Sale", saleId);
        if (sale.CustomerId is null || sale.Status is not
            ("Completed" or "PartiallyReturned" or "Returned")) return null;
        var card = await db.Cards.SingleOrDefaultAsync(x => x.CustomerId == sale.CustomerId, ct);
        if (card is null || sale.SaleDate < card.CreatedAt) return null;

        var earned = await db.Transactions.SingleOrDefaultAsync(
            x => x.SourceType == "Sale" && x.SourceId == saleId, ct);
        var changed = false;
        if (earned is null)
        {
            var program = await db.Programs.AsNoTracking().SingleOrDefaultAsync(ct);
            if (program is null || !program.IsActive || !card.IsActive) return Map(card);
            if (sale.CompletedBy is null || sale.CompletedBy == Guid.Empty)
                throw new ConflictException("Completed sale has no responsible user.");
            var points = PointsFor(sale.Total, program.SpendAmountPerPoint);
            earned = new LoyaltyTransaction(card.Id, LoyaltyTransactionType.SaleEarned,
                points, "Sale", sale.Id, sale.CompletedBy.Value,
                program.SpendAmountPerPoint);
            db.Transactions.Add(earned);
            card.ApplyPoints(points);
            changed = true;
        }

        var rate = earned.SpendAmountPerPointSnapshot!.Value;
        var returnIds = sale.Returns.Select(x => x.Id).ToArray();
        var alreadyReversed = await db.Transactions.AsNoTracking()
            .Where(x => x.CardId == card.Id && x.SourceType == "Return" &&
                returnIds.Contains(x.SourceId))
            .ToDictionaryAsync(x => x.SourceId, ct);
        decimal cumulativeRefund = 0;
        var totalReversed = 0;
        foreach (var saleReturn in sale.Returns)
        {
            cumulativeRefund += saleReturn.RefundTotal;
            if (alreadyReversed.TryGetValue(saleReturn.Id, out var existing))
            {
                totalReversed += -existing.PointsDelta;
                continue;
            }
            var remainingSpend = Math.Max(0m, sale.Total - cumulativeRefund);
            var targetReverse = Math.Max(0, earned.PointsDelta - PointsFor(remainingSpend, rate));
            var reverse = Math.Max(0, targetReverse - totalReversed);
            var entry = new LoyaltyTransaction(card.Id, LoyaltyTransactionType.SaleReturned,
                -reverse, "Return", saleReturn.Id, saleReturn.CreatedBy);
            db.Transactions.Add(entry);
            card.ApplyPoints(-reverse);
            totalReversed += reverse;
            changed = true;
        }
        if (changed) await SaveAsync(ct);
        return Map(card);
    }

    private static int PointsFor(decimal amount, decimal rate)
    {
        var value = decimal.Floor(amount / rate);
        if (value > int.MaxValue)
            throw new ConflictException("Sale amount would exceed the loyalty point limit.");
        return (int)value;
    }

    private async Task<LoyaltyCard> LoadCardAsync(Guid id, CancellationToken ct) =>
        await db.Cards.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Loyalty card", id);

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Loyalty balance changed concurrently; reload and retry.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("CRM record or source was already processed; reload and retry.");
        }
    }

    private static LoyaltyProgramDto Map(LoyaltyProgram x) =>
        new(x.Id, x.Name, x.CurrencyCode, x.SpendAmountPerPoint, x.IsActive);
    private static LoyaltyCardDto Map(LoyaltyCard x) =>
        new(x.Id, x.CustomerId, x.CardNumber, x.PointsBalance, x.IsActive, x.CreatedAt);
    private static LoyaltyRewardDto Map(LoyaltyReward x) =>
        new(x.Id, x.Name, x.PointsCost, x.IsActive);
    private static LoyaltyTransactionDto Map(LoyaltyTransaction x) =>
        new(x.Id, x.CardId, x.Type.ToString(), x.PointsDelta,
            x.SourceType, x.SourceId, x.OccurredAt, x.ActorId, x.RewardNameSnapshot);
}
