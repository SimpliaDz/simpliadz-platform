using Simplia.Sales.Core.Entities;

namespace Simplia.Sales.UseCases.Services;

public interface IPaymentService
{
    Task<PaymentDto> RecordAsync(Guid paymentId, Guid saleId, decimal amount,
        PaymentMethod method, Guid receivedBy, CancellationToken ct,
        Guid? cashSessionId = null, string? reference = null);
    Task<PaymentDto> GetByIdAsync(Guid paymentId, CancellationToken ct);
    Task<IReadOnlyList<PaymentDto>> ListBySaleAsync(Guid saleId, CancellationToken ct);
}

public sealed record PaymentDto(Guid Id, Guid SaleId, decimal Amount, string PaymentMethod,
    DateTimeOffset PaidAt, Guid ReceivedBy, Guid? CashSessionId = null, string? Reference = null);
