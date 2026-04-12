using SimpliaDz.Identity.Contracts.Auth;

namespace SimpliaDz.Identity.UseCases.Auth.Abstractions;

public interface IIdentityAuthService
{
    Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
    Task<ForgotPasswordResponse> GeneratePasswordResetTokenAsync(string email, CancellationToken ct);
    Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct);
}
