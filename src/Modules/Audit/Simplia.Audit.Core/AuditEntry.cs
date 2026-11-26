namespace Simplia.Audit.Core;

public sealed class AuditEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public DateTimeOffset Timestamp { get; private set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; private set; }

    private AuditEntry() { }

    public AuditEntry(Guid userId, string action, string entityType, Guid entityId,
        string? oldValues, string? newValues, string? correlationId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (entityId == Guid.Empty) throw new ArgumentException("Entity is required.", nameof(entityId));
        UserId = userId;
        Action = Required(action, 100, nameof(action));
        EntityType = Required(entityType, 100, nameof(entityType));
        EntityId = entityId;
        OldValues = Optional(oldValues, 4000, nameof(oldValues));
        NewValues = Optional(newValues, 4000, nameof(newValues));
        CorrelationId = Optional(correlationId, 100, nameof(correlationId));
    }

    private static string Required(string value, int max, string name) =>
        Optional(value, max, name) ?? throw new ArgumentException("Value is required.", name);

    private static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (result.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", name);
        return result;
    }
}
