namespace Simplia.Sales.Core.Entities;

public enum CashSessionStatus { Open = 1, Closed = 2 }

public sealed class CashSession
{
    private const decimal MaxMoney = 99999999999999.9999m;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CashRegisterId { get; private set; }
    public CashRegister CashRegister { get; private set; } = default!;
    public CashSessionStatus Status { get; private set; } = CashSessionStatus.Open;
    public Guid OpenedBy { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; } = DateTimeOffset.UtcNow;
    public decimal OpeningCash { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public decimal? ExpectedCash { get; private set; }
    public decimal? CountedCash { get; private set; }
    public decimal? Difference => CountedCash - ExpectedCash;
    public byte[] RowVersion { get; private set; } = [];

    private CashSession() { }

    public CashSession(Guid cashRegisterId, Guid openedBy, decimal openingCash)
    {
        if (cashRegisterId == Guid.Empty) throw new ArgumentException("Register is required.", nameof(cashRegisterId));
        if (openedBy == Guid.Empty) throw new ArgumentException("Opening user is required.", nameof(openedBy));
        ValidateMoney(openingCash, nameof(openingCash));
        CashRegisterId = cashRegisterId;
        OpenedBy = openedBy;
        OpeningCash = openingCash;
    }

    public void Close(Guid closedBy, decimal countedCash, decimal expectedCash)
    {
        if (Status != CashSessionStatus.Open)
            throw new InvalidOperationException("Cash session is already closed.");
        if (closedBy == Guid.Empty) throw new ArgumentException("Closing user is required.", nameof(closedBy));
        ValidateMoney(countedCash, nameof(countedCash));
        ValidateMoney(expectedCash, nameof(expectedCash));
        ClosedBy = closedBy;
        ClosedAt = DateTimeOffset.UtcNow;
        CountedCash = countedCash;
        ExpectedCash = expectedCash;
        Status = CashSessionStatus.Closed;
    }

    private static void ValidateMoney(decimal value, string name)
    {
        if (value < 0 || value > MaxMoney || decimal.Round(value, 4) != value)
            throw new ArgumentOutOfRangeException(name);
    }
}
