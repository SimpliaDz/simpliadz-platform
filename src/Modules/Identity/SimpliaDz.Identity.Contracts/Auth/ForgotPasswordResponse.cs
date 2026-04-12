namespace SimpliaDz.Identity.Contracts.Auth;

/// <summary>
/// In production, the reset token should be sent via email, not returned here.
/// This is exposed for development / testing convenience only.
/// </summary>
public sealed record ForgotPasswordResponse(string ResetToken);
