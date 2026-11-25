using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.Audit.Core;
using SimpliaDz.Audit.Infrastructure.Data;
using SimpliaDz.Audit.UseCases;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;

namespace SimpliaDz.Audit.Infrastructure;

public sealed class AuditTrail(AuditDbContext db, ITenantContext tenant) : IAuditTrail
{
    private static readonly string[] ForbiddenFields =
        ["password", "token", "secret", "hash", "credential", "key", "salt"];

    public async Task AppendAsync(Guid userId, string action, string entityType,
        Guid entityId, IReadOnlyDictionary<string, object?>? oldValues,
        IReadOnlyDictionary<string, object?>? newValues, string? correlationId,
        CancellationToken ct)
    {
        EnsureEnabled();
        var entry = new AuditEntry(userId, action, entityType, entityId,
            Serialize(oldValues), Serialize(newValues), correlationId);
        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuditHistoryDto> QueryAsync(Guid? userId, string? action,
        string? entityType, Guid? entityId, DateTimeOffset? from,
        DateTimeOffset? to, int page, int pageSize, CancellationToken ct)
    {
        EnsureEnabled();
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (from > to) throw new ArgumentException("From must be before to.", nameof(from));
        var query = db.Entries.AsNoTracking().AsQueryable();
        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action.Trim());
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(x => x.EntityType == entityType.Trim());
        else
            query = query.Where(x => x.EntityType != "ApiMutation");
        if (entityId.HasValue) query = query.Where(x => x.EntityId == entityId.Value);
        if (from.HasValue) query = query.Where(x => x.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(x => x.Timestamp <= to.Value);
        var total = await query.LongCountAsync(ct);
        var entries = await query.OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).Select(x => new AuditEntryDto(x.Id, x.UserId,
                x.Action, x.EntityType, x.EntityId, x.OldValues, x.NewValues,
                x.Timestamp, x.CorrelationId)).ToListAsync(ct);
        return new AuditHistoryDto(total, entries);
    }

    private void EnsureEnabled()
    {
        if (!tenant.IsModuleEnabled("Audit"))
            throw new ConflictException("Audit must be enabled for audited business actions.");
    }

    private static string? Serialize(IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null) return null;
        if (values.Keys.Any(key => ForbiddenFields.Any(forbidden =>
                key.Contains(forbidden, StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Sensitive authentication fields cannot be audited.", nameof(values));
        if (values.Values.Any(value => value is not null and not string and not Guid and
                not bool and not int and not long and not decimal and not double))
            throw new ArgumentException("Audit values must be simple scalar values.", nameof(values));
        return JsonSerializer.Serialize(values);
    }
}
