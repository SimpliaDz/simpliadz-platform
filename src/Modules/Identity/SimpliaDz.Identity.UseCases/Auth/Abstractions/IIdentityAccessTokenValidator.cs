namespace SimpliaDz.Identity.UseCases.Auth.Abstractions;

public interface IIdentityAccessTokenValidator
{
    Task<bool> IsValidAsync(Guid userId, string securityStamp, CancellationToken ct);
}
