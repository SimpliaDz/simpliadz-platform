namespace SimpliaDz.Organization.Core.Entities;

public sealed class WorkSession
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public DateTimeOffset ClockInAt { get; private set; }
    public DateTimeOffset? ClockOutAt { get; private set; }
    public Guid? ClockedOutBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private WorkSession() { }

    public WorkSession(Guid userId, DateTimeOffset clockInAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        UserId = userId;
        ClockInAt = clockInAt;
        CreatedAt = clockInAt;
        UpdatedAt = clockInAt;
    }

    public void ClockOut(Guid actorId, DateTimeOffset at)
    {
        if (ClockOutAt.HasValue) throw new InvalidOperationException("Work session is already closed.");
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (at <= ClockInAt) throw new ArgumentException("Clock-out must follow clock-in.", nameof(at));
        ClockOutAt = at;
        ClockedOutBy = actorId;
        UpdatedAt = at;
    }

    public void Correct(DateTimeOffset clockInAt, DateTimeOffset? clockOutAt,
        DateTimeOffset correctedAt)
    {
        if (clockOutAt.HasValue && clockOutAt.Value <= clockInAt)
            throw new ArgumentException("Clock-out must follow clock-in.", nameof(clockOutAt));
        if (ClockOutAt.HasValue && !clockOutAt.HasValue)
            throw new ArgumentException("A closed session cannot be reopened.", nameof(clockOutAt));
        ClockInAt = clockInAt;
        ClockOutAt = clockOutAt;
        UpdatedAt = correctedAt;
    }
}
