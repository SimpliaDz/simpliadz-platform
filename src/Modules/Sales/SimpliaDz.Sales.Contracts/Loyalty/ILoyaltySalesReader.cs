namespace SimpliaDz.Sales.Contracts.Loyalty;

public sealed record LoyaltyReturnSnapshot(Guid Id, decimal RefundTotal, Guid CreatedBy);

public sealed record LoyaltySaleSnapshot(Guid Id, Guid? CustomerId, Guid? CompletedBy,
    string Status, DateTimeOffset SaleDate,
    decimal Total, IReadOnlyList<LoyaltyReturnSnapshot> Returns);

public interface ILoyaltySalesReader
{
    Task<bool?> IsCustomerActiveAsync(Guid customerId, CancellationToken ct);
    Task<LoyaltySaleSnapshot?> GetSaleAsync(Guid saleId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetCompletedSaleIdsAsync(Guid customerId, CancellationToken ct);
}
