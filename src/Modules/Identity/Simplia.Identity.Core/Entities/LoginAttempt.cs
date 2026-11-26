namespace Simplia.Identity.Core.Entities;

public sealed class LoginAttempt
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private LoginAttempt() { }

    public LoginAttempt(Guid? userId, string email, bool succeeded,
        string? ipAddress, string? userAgent)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID must be valid when supplied.", nameof(userId));
        if (succeeded && userId is null)
            throw new ArgumentException("A successful login requires a user ID.", nameof(userId));
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 256)
            throw new ArgumentException("Attempted email is required and must fit 256 characters.", nameof(email));

        UserId = userId;
        Email = email.Trim().ToLowerInvariant();
        Succeeded = succeeded;
        IpAddress = Normalize(ipAddress, 45);
        UserAgent = Normalize(userAgent, 512);
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
