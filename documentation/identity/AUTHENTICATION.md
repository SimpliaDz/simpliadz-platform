# Authentication and session lifecycle

The Identity module authenticates users inside the API instance's configured tenant. Public self-registration is intentionally not supported: tenant administrators create users today, and a future invitation workflow can replace password-based creation without changing the tenant database boundary.

## Available endpoints

| Endpoint | Authentication | Purpose |
|---|---|---|
| `POST /auth/login` | Public | Verify credentials and issue an access token plus refresh token. |
| `POST /auth/refresh` | Public | Rotate a valid refresh token and issue a new token pair. |
| `POST /auth/logout` | Public | Revoke a refresh token. It remains usable when the access token has expired. |
| `GET /auth/me` | Bearer token | Return the current token identity, roles, and permissions. |
| `POST /auth/change-password` | Bearer token | Change the current user's password and revoke all sessions. |
| `POST /auth/forgot-password` | Public | Generate a reset token. The token is returned only when the development-only exposure option is enabled. |
| `POST /auth/reset-password` | Public | Reset a password with a valid ASP.NET Identity reset token and revoke all sessions. |

User, role, and permission management remains under `/users`, `/roles`, and `/permissions` and is protected by permission policies.

## Token behavior

- Access tokens are signed JWTs. They contain the user ID, email, tenant ID, roles, permissions, security stamp, and a unique token ID.
- Access-token lifetime is configured with `Jwt:ExpiresMinutes` (60 minutes by default).
- Refresh-token lifetime is configured with `Jwt:RefreshTokenExpiresDays` (14 days by default).
- Only a SHA-256 hash of each refresh token is stored in `identity.RefreshTokens`; the raw token is returned once to the client.
- Refresh tokens rotate on every refresh. Reusing a rotated token is treated as possible theft and revokes the user's remaining refresh-token family.
- A SQL row-version prevents two concurrent requests from successfully rotating the same token.
- Logout is idempotent and does not reveal whether the supplied token existed.

Clients should keep access tokens in memory and store refresh tokens using the platform's secure credential storage. A browser application should prefer a Secure, HttpOnly, SameSite cookie when its deployment model allows it; the current API returns the refresh token in JSON so non-browser clients can also use it.

## Session invalidation

JWT validation checks the configured tenant and the user's current Identity security stamp on every tenant API request. Existing access and refresh tokens stop working when:

- the password is changed or reset;
- the user is activated or deactivated;
- roles are assigned to or removed from the user;
- permissions assigned to one of the user's roles change; or
- refresh-token reuse is detected.

Five failed password attempts lock the account for 15 minutes. Login uses the same generic failure message for unknown users, bad passwords, inactive accounts, and locked accounts.

## Password reset during development

`Identity:ExposePasswordResetToken` is `false` by default and `true` only in `appsettings.Development.json`. Production must leave it disabled. Once the Notifications module is implemented, it should deliver the generated token through the tenant's configured email provider; the public response should remain generic to prevent account enumeration.

## Database deployment

Run the database migrator for each tenant before deploying the updated API. Migration `Identity_RefreshTokens` creates the refresh-token table and indexes.

```powershell
dotnet run --project src/SimpliaDz.DatabaseMigrator -- --tenant dev
```

## Intentionally deferred capabilities

- invitation acceptance and initial-password setup;
- email delivery for password reset and invitations;
- email-address confirmation;
- multi-factor authentication and recovery codes;
- external identity providers and single sign-on.

These are product decisions or depend on the future Notifications/provisioning work. They should not be represented as already-complete authentication behavior.
