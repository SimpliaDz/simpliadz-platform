using SimpliaDz.Sales.Core.Entities;

namespace SimpliaDz.Sales.UseCases.Services;

public interface ISaleService
{
    Task<SaleDto> CreateDraftAsync(Guid locationId, Guid? customerId, Guid cashierId,
        Guid priceListId, CancellationToken ct);
    Task<SaleDto> AddLineAsync(Guid saleId, string? sku, string? barcode, int quantity,
        decimal discountAmount, CancellationToken ct, bool checkStock = false);
    Task<SaleDto> SetCustomerAsync(Guid saleId, Guid? customerId, CancellationToken ct);
    Task<SaleDto> SetLineQuantityAsync(Guid saleId, Guid lineId, int quantity, CancellationToken ct,
        bool checkStock = false);
    Task<SaleDto> RemoveLineAsync(Guid saleId, Guid lineId, CancellationToken ct);
    Task<SaleDto> SetDiscountAsync(Guid saleId, decimal discountAmount, CancellationToken ct);
    Task<SaleDto> ApplySaleDiscountAsync(Guid saleId, SaleDiscountType type,
        decimal value, CancellationToken ct);
    Task<SaleDto> ApplyLineDiscountAsync(Guid saleId, Guid lineId,
        SaleDiscountType type, decimal value, CancellationToken ct);
    Task<SaleDto> CompleteAsync(Guid saleId, Guid completedBy, CancellationToken ct);
    Task CancelAsync(Guid saleId, CancellationToken ct);
    Task<SaleDto> GetByIdAsync(Guid saleId, CancellationToken ct);
    Task<IReadOnlyList<SaleDto>> ListAsync(Guid? locationId, Guid? customerId,
        string? status, string? search, int page, int pageSize, CancellationToken ct);
}

public sealed record SaleLineDto(Guid Id, Guid ProductVariantId, int Quantity,
    decimal UnitPrice, decimal UnitCostSnapshot, decimal DiscountAmount, decimal LineTotal,
    string? DiscountType, decimal? DiscountValue, int ReturnedQuantity)
{
    public string? ProductName { get; init; }
    public string? VariantName { get; init; }
    public string? Sku { get; init; }
    public int? AvailableQuantity { get; init; }
}

public sealed record SaleDto(Guid Id, string? SaleNumber, Guid LocationId, Guid? CustomerId,
    Guid CashierId, Guid PriceListId, string Status, DateTimeOffset SaleDate,
    decimal Subtotal, decimal DiscountAmount, decimal Total, IReadOnlyList<SaleLineDto> Lines,
    string? DiscountType, decimal? DiscountValue, decimal PaidAmount, decimal BalanceDue);
