using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Organization.Core.Entities;
using Simplia.Organization.Infrastructure.Data;
using OrganizationEntity = Simplia.Organization.Core.Entities.Organization;

namespace Simplia.Api.Modules;

public sealed class WorkSessionService(OrganizationDbContext db, TimeProvider clock)
{
    public async Task<WorkSessionDto> ClockInAsync(Guid userId, CancellationToken ct)
    {
        var session = new WorkSession(userId, clock.GetUtcNow());
        db.WorkSessions.Add(session);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("User already has an open work session.");
        }
        return Map(session);
    }

    public async Task<WorkSessionDto> ClockOutAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var session = await LoadAsync(id, ct);
        if (session.UserId != userId) throw new UnauthorizedAccessException("This is another user's work session.");
        if (session.ClockOutAt.HasValue) throw new ConflictException("Work session is already closed.");
        session.ClockOut(userId, clock.GetUtcNow());
        await SaveWithConcurrencyAsync(ct);
        return Map(session);
    }

    public async Task<WorkSessionDto> CorrectAsync(Guid id, DateTimeOffset clockInAt,
        DateTimeOffset? clockOutAt, string reason, Guid correctedBy, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var session = await LoadAsync(id, ct);
        var proposedEnd = clockOutAt ?? DateTimeOffset.MaxValue;
        if (await db.WorkSessions.AnyAsync(x => x.UserId == session.UserId &&
                x.Id != id && x.ClockInAt < proposedEnd &&
                (x.ClockOutAt == null || x.ClockOutAt > clockInAt), ct))
            throw new ConflictException("Corrected work session would overlap another session for this user.");
        var correction = new WorkSessionCorrection(id, session.ClockInAt,
            session.ClockOutAt, clockInAt, clockOutAt, reason, correctedBy, clock.GetUtcNow());
        session.Correct(clockInAt, clockOutAt, correction.CorrectedAt);
        db.WorkSessionCorrections.Add(correction);
        await SaveWithConcurrencyAsync(ct);
        await tx.CommitAsync(ct);
        return Map(session);
    }

    public async Task<IReadOnlyList<WorkSessionDto>> ListAsync(Guid? userId,
        DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken ct)
    {
        ValidateRange(fromDate, toDate, page, pageSize);
        var query = db.WorkSessions.AsNoTracking().AsQueryable();
        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (fromDate.HasValue)
        {
            var from = new DateTimeOffset(fromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.ClockInAt >= from);
        }
        if (toDate.HasValue)
        {
            var to = new DateTimeOffset(toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.ClockInAt < to);
        }
        return (await query.OrderByDescending(x => x.ClockInAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct))
            .Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<WorkSessionCorrection>> CorrectionsAsync(Guid id,
        CancellationToken ct)
    {
        if (!await db.WorkSessions.AnyAsync(x => x.Id == id, ct))
            throw new NotFoundException("Work session", id);
        return await db.WorkSessionCorrections.AsNoTracking()
            .Where(x => x.WorkSessionId == id).OrderBy(x => x.CorrectedAt)
            .ToListAsync(ct);
    }

    public async Task<WorkHoursReportDto> ReportAsync(Guid? userId,
        DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        if (userId == Guid.Empty || fromDate == default || toDate == default ||
            toDate == DateOnly.MaxValue || toDate < fromDate ||
            toDate.DayNumber - fromDate.DayNumber > 366)
            throw new ArgumentException("User and date range must be valid and cover at most 366 days.");
        var organization = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(ct);
        var zoneId = organization?.TimeZoneId ?? OrganizationEntity.DefaultTimeZoneId;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var sessionsQuery = db.WorkSessions.AsNoTracking()
            .Where(x => x.ClockOutAt != null && x.ClockInAt < toUtc && x.ClockOutAt > fromUtc);
        if (userId.HasValue) sessionsQuery = sessionsQuery.Where(x => x.UserId == userId.Value);
        var sessions = await sessionsQuery.ToListAsync(ct);
        var minutes = new Dictionary<(Guid UserId, DateOnly Date), decimal>();
        foreach (var session in sessions)
        {
            var start = session.ClockInAt.UtcDateTime > fromUtc ? session.ClockInAt.UtcDateTime : fromUtc;
            var end = session.ClockOutAt!.Value.UtcDateTime < toUtc ?
                session.ClockOutAt.Value.UtcDateTime : toUtc;
            while (start < end)
            {
                var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(start, zone));
                var nextMidnight = TimeZoneInfo.ConvertTimeToUtc(
                    date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
                var segmentEnd = end < nextMidnight ? end : nextMidnight;
                var key = (session.UserId, date);
                minutes[key] = minutes.GetValueOrDefault(key) +
                    (decimal)(segmentEnd - start).TotalMinutes;
                start = segmentEnd;
            }
        }
        var rows = minutes.OrderBy(x => x.Key.Date).ThenBy(x => x.Key.UserId)
            .Select(x => new WorkHoursDayDto(x.Key.UserId, x.Key.Date,
                Math.Round(x.Value / 60m, 2))).ToArray();
        return new WorkHoursReportDto(fromDate, toDate, zoneId,
            Math.Round(minutes.Values.Sum() / 60m, 2), rows);
    }

    private async Task<WorkSession> LoadAsync(Guid id, CancellationToken ct) =>
        await db.WorkSessions.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Work session", id);

    private async Task SaveWithConcurrencyAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("Work session changed concurrently. Reload it and retry.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("User already has an open work session.");
        }
    }

    private static void ValidateRange(DateOnly? fromDate, DateOnly? toDate,
        int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize ||
            toDate == DateOnly.MaxValue ||
            (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value))
            throw new ArgumentException("Date range or pagination is invalid.");
    }

    private static WorkSessionDto Map(WorkSession x) => new(x.Id, x.UserId,
        x.ClockInAt, x.ClockOutAt, x.ClockedOutBy, x.CreatedAt, x.UpdatedAt);
}

public sealed record WorkSessionDto(Guid Id, Guid UserId, DateTimeOffset ClockInAt,
    DateTimeOffset? ClockOutAt, Guid? ClockedOutBy, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
public sealed record WorkHoursDayDto(Guid UserId, DateOnly Date, decimal Hours);
public sealed record WorkHoursReportDto(DateOnly FromDate, DateOnly ToDate,
    string TimeZoneId, decimal TotalHours, IReadOnlyList<WorkHoursDayDto> Days);
