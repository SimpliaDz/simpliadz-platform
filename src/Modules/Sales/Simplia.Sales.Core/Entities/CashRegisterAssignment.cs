namespace Simplia.Sales.Core.Entities;

/// <summary>Records which clocked-in cashier is actively operating a physical register.</summary>
public sealed class CashRegisterAssignment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CashRegisterId { get; private set; }
    public CashRegister CashRegister { get; private set; } = default!;
    public Guid CashSessionId { get; private set; }
    public CashSession CashSession { get; private set; } = default!;
    public Guid UserId { get; private set; }
    public Guid WorkSessionId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAt { get; private set; }

    private CashRegisterAssignment() { }

    public CashRegisterAssignment(Guid cashRegisterId, Guid cashSessionId, Guid userId,
        Guid workSessionId)
    {
        if (cashRegisterId == Guid.Empty || cashSessionId == Guid.Empty || userId == Guid.Empty ||
            workSessionId == Guid.Empty)
            throw new ArgumentException("Register, session, user, and work session are required.");
        CashRegisterId = cashRegisterId;
        CashSessionId = cashSessionId;
        UserId = userId;
        WorkSessionId = workSessionId;
    }

    public void Release()
    {
        if (ReleasedAt.HasValue) throw new InvalidOperationException("Register assignment is already released.");
        ReleasedAt = DateTimeOffset.UtcNow;
    }
}
