# Authentication and session lifecycle

The Identity module authenticates users inside the API instance's configured tenant. Public self-registration is intentionally not supported: tenant administrators create users today, and a future invitation workflow can replace password-based creation without changing the tenant database boundary.

## Available endpoints

| Endpoint | Authentication | Purpose |
|---|---|---|
| `POST /auth/login` | Public | Verify credentials and issue an access token plus refresh token. |
| `GET /auth/me/login-attempts` | Bearer token | View the current user's password login history. |
| `GET /auth/login-attempts` | `login-attempts.view` | Search tenant password login history. |
| `POST /auth/refresh` | Public | Rotate a valid refresh token and issue a new token pair. |
| `POST /auth/logout` | Public | Revoke a refresh token. It remains usable when the access token has expired. |
| `GET /auth/me` | Bearer token | Return the current token identity, roles, and permissions. |
| `PUT /auth/me/language` | Bearer token | Set the current user's language preference. |
| `POST /auth/change-password` | Bearer token | Change the current user's password and revoke all sessions. |
| `POST /auth/forgot-password` | Public | Generate a reset token. The token is returned only when the development-only exposure option is enabled. |
| `POST /auth/reset-password` | Public | Reset a password with a valid ASP.NET Identity reset token and revoke all sessions. |

User, role, and permission management remains under `/users`, `/roles`, and `/permissions` and is protected by permission policies.

## User language preference

Each user has a `languageCode` stored in the tenant's Identity database. Supported values are `en`, `ar`, and `fr`. New and existing users default to `en`. `GET /auth/me` returns the saved value alongside the user's identity and permissions; user management responses also include it. An authenticated user can change only their own preference:

```http
PUT /auth/me/language
Authorization: Bearer <access-token>
Content-Type: application/json

{"languageCode":"fr"}
```

The response is `200 OK` with `{"languageCode":"fr"}`. Codes are case-insensitive on input and stored in lowercase. Unsupported or empty codes return a validation error. The frontend should load `/auth/me` after sign-in and use this value to select its UI language. Updating the preference does not invalidate the session; the next `/auth/me` request reflects the new value. This preference controls client presentation and does not change server timestamps or the organization's default language.

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

## Password login history

Every valid `POST /auth/login` request creates an append-only row in `identity.LoginAttempts`, whether authentication succeeds or fails. It records the attempted email, outcome, UTC time, optional matching user ID, remote IP address, and user agent. It does not store passwords or tokens. Unknown emails have no user ID. Refreshing a token does not count as a password login.

The authenticated user can read their own attempts at `GET /auth/me/login-attempts`. An administrator with `login-attempts.view` can query `GET /auth/login-attempts` using optional `userId`, `email`, `succeeded`, `from`, and `to` filters. Both endpoints accept `page` and `pageSize` (maximum 100). These records are separate from ASP.NET Identity's `identity.AspNetUserLogins`, which stores external-provider sign-ins and is expected to stay empty for password login from Postman or the mobile app.

When Audit is enabled for the tenant, each successful password login also creates a `UserLoggedIn` business audit entry. The audit event stores only the authentication method, user ID, timestamp, and correlation ID. Login history remains available when Audit is disabled.

## Password reset during development

`Identity:ExposePasswordResetToken` is `false` by default and `true` only in `appsettings.Development.json`. Production must leave it disabled. Once the Notifications module is implemented, it should deliver the generated token through the tenant's configured email provider; the public response should remain generic to prevent account enumeration.

## Database deployment

Run the database migrator for each tenant before deploying the updated API. Migration `Identity_UserLanguage` adds the user preference with an `en` default; `Identity_LoginAttempts` creates the login-history table and indexes; earlier migration `Identity_RefreshTokens` creates the refresh-token table.

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
