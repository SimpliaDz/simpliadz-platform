namespace Simplia.Identity.UseCases.Auth.Abstractions;

public sealed record LoginAttemptDto(Guid Id, Guid? UserId, string Email,
    bool Succeeded, DateTimeOffset OccurredAt, string? IpAddress,
    string? UserAgent);

public sealed record LoginAttemptPage(long TotalItems,
    IReadOnlyList<LoginAttemptDto> Items);

public interface ILoginAttemptReader
{
    Task<LoginAttemptPage> QueryAsync(Guid? userId, string? email,
        bool? succeeded, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, CancellationToken ct);
}
