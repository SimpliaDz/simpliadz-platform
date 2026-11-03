namespace SimpliaDz.Sales.UseCases.Services;

public sealed record ReturnSaleLine(Guid OriginalSaleLineId, int Quantity, bool Restock);

public sealed record SaleReturnLineDto(Guid Id, Guid OriginalSaleLineId,
    Guid ProductVariantId, int Quantity, decimal UnitRefundAmount,
    decimal RefundAmount, bool Restock);

public sealed record SaleReturnDto(Guid Id, string ReturnNumber, Guid OriginalSaleId,
    Guid LocationId, DateTimeOffset ReturnDate, string Reason, Guid CreatedBy,
    decimal RefundTotal, IReadOnlyList<SaleReturnLineDto> Lines);

public interface ISaleReturnService
{
    Task<SaleReturnDto> ProcessAsync(Guid id, Guid saleId, Guid locationId,
        string reason, Guid createdBy, IReadOnlyList<ReturnSaleLine> lines,
        CancellationToken ct);
    Task<SaleReturnDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SaleReturnDto>> ListBySaleAsync(Guid saleId, CancellationToken ct);
}
