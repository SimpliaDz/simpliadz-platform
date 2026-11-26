namespace Simplia.Purchasing.Core.Entities;

public enum SupplierPaymentMethod { Cash = 1, Card = 2, BankTransfer = 3, Cheque = 4, Other = 5 }

public sealed class SupplierPayment
{
    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public decimal Amount { get; private set; }
    public SupplierPaymentMethod Method { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    private SupplierPayment() { }

    public SupplierPayment(Guid id, Guid orderId, decimal amount, SupplierPaymentMethod method,
        DateOnly date, Guid actor, string? reference, string? notes)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || actor == Guid.Empty)
            throw new ArgumentException("Payment ID, order and actor are required.");
        if (amount <= 0 || amount > 99999999999999.9999m || decimal.Round(amount, 4) != amount)
            throw new ArgumentException("Payment amount must be positive and fit decimal(18,4).");
        if (!Enum.IsDefined(method) || date == default)
            throw new ArgumentException("Payment method and date are required.");
        Id = id;
        PurchaseOrderId = orderId;
        Amount = amount;
        Method = method;
        PaymentDate = date;
        RecordedAt = DateTimeOffset.UtcNow;
        RecordedBy = actor;
        Reference = Normalize(reference, 100);
        Notes = Normalize(notes, 500);
    }

    private static string? Normalize(string? text, int maximum) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().Length <= maximum ? text.Trim()
            : throw new ArgumentException($"Payment text cannot exceed {maximum} characters.");
}
