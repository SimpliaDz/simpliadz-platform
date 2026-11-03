using SimpliaDz.Sales.Core.Entities;

namespace SimpliaDz.Sales.UseCases.Services;

public interface IPaymentService
{
    Task<PaymentDto> RecordAsync(Guid paymentId, Guid saleId, decimal amount,
        PaymentMethod method, Guid receivedBy, CancellationToken ct);
    Task<PaymentDto> GetByIdAsync(Guid paymentId, CancellationToken ct);
    Task<IReadOnlyList<PaymentDto>> ListBySaleAsync(Guid saleId, CancellationToken ct);
}

public sealed record PaymentDto(Guid Id, Guid SaleId, decimal Amount, string PaymentMethod,
    DateTimeOffset PaidAt, Guid ReceivedBy);
