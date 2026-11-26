namespace Simplia.Crm.Core.Entities;

public enum LoyaltyTransactionType { SaleEarned = 1, SaleReturned = 2, RewardRedeemed = 3 }

public sealed class LoyaltyTransaction
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CardId { get; private set; }
    public LoyaltyTransactionType Type { get; private set; }
    public int PointsDelta { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public decimal? SpendAmountPerPointSnapshot { get; private set; }
    public string? RewardNameSnapshot { get; private set; }
    public Guid? RewardId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid ActorId { get; private set; }

    private LoyaltyTransaction() { }

    public LoyaltyTransaction(Guid cardId, LoyaltyTransactionType type, int pointsDelta,
        string sourceType, Guid sourceId, Guid actorId, decimal? rateSnapshot = null,
        string? rewardNameSnapshot = null, Guid? rewardId = null)
    {
        if (cardId == Guid.Empty || sourceId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Card, source, and actor are required.");
        if (!Enum.IsDefined(type))
            throw new ArgumentException("A valid transaction type is required.");
        if (type == LoyaltyTransactionType.SaleEarned && (pointsDelta < 0 || rateSnapshot <= 0))
            throw new ArgumentException("Sale earnings need a nonnegative point change and the earning rate.");
        if (type != LoyaltyTransactionType.SaleEarned && pointsDelta > 0)
            throw new ArgumentException("Returns and rewards must debit points.");
        if (type == LoyaltyTransactionType.RewardRedeemed &&
            (pointsDelta >= 0 || rewardId is null || string.IsNullOrWhiteSpace(rewardNameSnapshot)))
            throw new ArgumentException("Reward redemption needs a reward and a negative point change.");
        if (string.IsNullOrWhiteSpace(sourceType) || sourceType.Length > 30)
            throw new ArgumentException("Source type is required.", nameof(sourceType));
        CardId = cardId;
        Type = type;
        PointsDelta = pointsDelta;
        SourceType = sourceType;
        SourceId = sourceId;
        ActorId = actorId;
        SpendAmountPerPointSnapshot = rateSnapshot;
        RewardNameSnapshot = rewardNameSnapshot;
        RewardId = rewardId;
    }
}
