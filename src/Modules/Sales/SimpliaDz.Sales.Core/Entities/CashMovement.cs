namespace SimpliaDz.Sales.Core.Entities;

public sealed class CashMovement
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CashSessionId { get; private set; }
    public CashSession CashSession { get; private set; } = default!;
    public decimal AmountDelta { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;

    private CashMovement() { }

    public CashMovement(Guid cashSessionId, decimal amountDelta, string reason, Guid createdBy)
    {
        if (cashSessionId == Guid.Empty) throw new ArgumentException("Session is required.", nameof(cashSessionId));
        if (createdBy == Guid.Empty) throw new ArgumentException("User is required.", nameof(createdBy));
        if (amountDelta == 0 || amountDelta < -99999999999999.9999m ||
            amountDelta > 99999999999999.9999m ||
            decimal.Round(amountDelta, 4) != amountDelta)
            throw new ArgumentOutOfRangeException(nameof(amountDelta));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("Reason is required and cannot exceed 500 characters.", nameof(reason));
        CashSessionId = cashSessionId;
        AmountDelta = amountDelta;
        Reason = reason.Trim();
        CreatedBy = createdBy;
    }
}
