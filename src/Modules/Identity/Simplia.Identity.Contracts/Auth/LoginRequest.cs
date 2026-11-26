namespace Simplia.Identity.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);