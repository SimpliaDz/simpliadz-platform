namespace SimpliaDz.Sales.Core.Entities;

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3,
    Other = 4
}

public sealed class Payment
{
    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public Sale Sale { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public DateTimeOffset PaidAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid ReceivedBy { get; private set; }
    public Guid? CashSessionId { get; private set; }
    public CashSession? CashSession { get; private set; }

    private Payment() { }

    public Payment(Guid id, Guid saleId, decimal amount, PaymentMethod paymentMethod,
        Guid receivedBy, Guid? cashSessionId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Payment ID is required.", nameof(id));
        if (saleId == Guid.Empty) throw new ArgumentException("Sale is required.", nameof(saleId));
        if (amount <= 0 || amount > 99999999999999.9999m ||
            decimal.Round(amount, 4) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Enum.IsDefined(paymentMethod))
            throw new ArgumentOutOfRangeException(nameof(paymentMethod));
        if (receivedBy == Guid.Empty)
            throw new ArgumentException("Receiving user is required.", nameof(receivedBy));
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session ID must be valid.", nameof(cashSessionId));
        Id = id;
        SaleId = saleId;
        Amount = amount;
        PaymentMethod = paymentMethod;
        ReceivedBy = receivedBy;
        CashSessionId = cashSessionId;
    }
}
