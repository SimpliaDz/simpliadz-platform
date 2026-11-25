namespace SimpliaDz.Audit.UseCases;

public sealed record AuditEntryDto(Guid Id, Guid UserId, string Action,
    string EntityType, Guid EntityId, string? OldValues, string? NewValues,
    DateTimeOffset Timestamp, string? CorrelationId)
{
    public string? ActorName { get; init; }
}

public sealed record AuditHistoryDto(long TotalItems, IReadOnlyList<AuditEntryDto> Items);

public interface IAuditTrail
{
    Task AppendAsync(Guid userId, string action, string entityType, Guid entityId,
        IReadOnlyDictionary<string, object?>? oldValues,
        IReadOnlyDictionary<string, object?>? newValues,
        string? correlationId, CancellationToken ct);

    Task<AuditHistoryDto> QueryAsync(Guid? userId, string? action,
        string? entityType, Guid? entityId, DateTimeOffset? from,
        DateTimeOffset? to, int page, int pageSize, CancellationToken ct);
}
