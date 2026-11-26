using Microsoft.EntityFrameworkCore;
using Simplia.Identity.Infrastructure.Data;
using Simplia.Identity.UseCases.Auth.Abstractions;

namespace Simplia.Identity.Infrastructure.Auth;

public sealed class LoginAttemptReader(IdentityDbContext db) : ILoginAttemptReader
{
    public async Task<LoginAttemptPage> QueryAsync(Guid? userId, string? email,
        bool? succeeded, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, CancellationToken ct)
    {
        if (userId == Guid.Empty || page < 1 || pageSize is < 1 or > 100 ||
            page > int.MaxValue / pageSize || from > to || email?.Length > 256)
            throw new ArgumentException("Login history filters or pagination are invalid.");

        var query = db.LoginAttempts.AsNoTracking().AsQueryable();
        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLowerInvariant();
            query = query.Where(x => x.Email == normalized);
        }
        if (succeeded.HasValue) query = query.Where(x => x.Succeeded == succeeded.Value);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to.Value);

        var total = await query.LongCountAsync(ct);
        var rows = await query.OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).Select(x => new LoginAttemptDto(x.Id, x.UserId,
                x.Email, x.Succeeded, x.OccurredAt, x.IpAddress,
                x.UserAgent)).ToListAsync(ct);
        return new LoginAttemptPage(total, rows);
    }
}
