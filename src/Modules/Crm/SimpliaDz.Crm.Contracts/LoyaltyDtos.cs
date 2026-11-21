namespace SimpliaDz.Crm.Contracts;

public sealed record LoyaltyProgramDto(Guid Id, string Name, string CurrencyCode,
    decimal SpendAmountPerPoint, bool IsActive);

public sealed record LoyaltyCardDto(Guid Id, Guid CustomerId, string CardNumber,
    int PointsBalance, bool IsActive, DateTimeOffset CreatedAt);

public sealed record LoyaltyRewardDto(Guid Id, string Name, int PointsCost, bool IsActive);

public sealed record LoyaltyTransactionDto(Guid Id, Guid CardId, string Type,
    int PointsDelta, string SourceType, Guid SourceId, DateTimeOffset OccurredAt,
    Guid ActorId, string? RewardNameSnapshot);
