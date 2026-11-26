namespace Simplia.Organization.Core.Entities;

public sealed class WorkSessionCorrection
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkSessionId { get; private set; }
    public DateTimeOffset OldClockInAt { get; private set; }
    public DateTimeOffset? OldClockOutAt { get; private set; }
    public DateTimeOffset NewClockInAt { get; private set; }
    public DateTimeOffset? NewClockOutAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid CorrectedBy { get; private set; }
    public DateTimeOffset CorrectedAt { get; private set; }

    private WorkSessionCorrection() { }

    public WorkSessionCorrection(Guid workSessionId, DateTimeOffset oldClockInAt,
        DateTimeOffset? oldClockOutAt, DateTimeOffset newClockInAt,
        DateTimeOffset? newClockOutAt, string reason, Guid correctedBy,
        DateTimeOffset correctedAt)
    {
        if (workSessionId == Guid.Empty || correctedBy == Guid.Empty)
            throw new ArgumentException("Work session and correcting user are required.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("Correction reason is required (maximum 500 characters).", nameof(reason));
        WorkSessionId = workSessionId;
        OldClockInAt = oldClockInAt;
        OldClockOutAt = oldClockOutAt;
        NewClockInAt = newClockInAt;
        NewClockOutAt = newClockOutAt;
        Reason = reason.Trim();
        CorrectedBy = correctedBy;
        CorrectedAt = correctedAt;
    }
}
