using SimpliaDz.Crm.Contracts;

namespace SimpliaDz.Crm.UseCases.Loyalty;

public interface ILoyaltyService
{
    Task<LoyaltyProgramDto?> GetProgramAsync(CancellationToken ct);
    Task<LoyaltyProgramDto> ConfigureProgramAsync(string name, decimal spendAmountPerPoint,
        bool isActive, CancellationToken ct);
    Task<LoyaltyCardDto> EnrollAsync(Guid customerId, CancellationToken ct);
    Task<LoyaltyCardDto> GetCardByCustomerAsync(Guid customerId, CancellationToken ct);
    Task<LoyaltyCardDto> GetCardByIdAsync(Guid cardId, CancellationToken ct);
    Task<LoyaltyCardDto> GetCardByNumberAsync(string cardNumber, CancellationToken ct);
    Task<LoyaltyCardDto> SetCardActiveAsync(Guid cardId, bool active, CancellationToken ct);
    Task<IReadOnlyList<LoyaltyTransactionDto>> GetHistoryAsync(Guid cardId,
        int page, int pageSize, CancellationToken ct);
    Task<LoyaltyRewardDto> CreateRewardAsync(string name, int pointsCost, CancellationToken ct);
    Task<LoyaltyRewardDto> GetRewardByIdAsync(Guid id, CancellationToken ct);
    Task<LoyaltyRewardDto> UpdateRewardAsync(Guid id, string name, int pointsCost,
        bool isActive, CancellationToken ct);
    Task<IReadOnlyList<LoyaltyRewardDto>> ListRewardsAsync(bool includeInactive,
        CancellationToken ct);
    Task<LoyaltyTransactionDto> RedeemAsync(Guid cardId, Guid rewardId,
        Guid redemptionId, Guid actorId, CancellationToken ct);
    Task<LoyaltyCardDto?> SyncSaleAsync(Guid saleId, CancellationToken ct);
}
