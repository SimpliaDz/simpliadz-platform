# Business Audit Trail (SMP-52)

The Audit module stores tenant-isolated, append-only `AuditEntry` rows in the tenant database. Run the database migrator for every tenant after enabling the `Audit` module. The local `dev` and `demo` tenants now have it enabled. Audited API actions require Audit to be enabled before the handler runs.

Each entry stores `id`, authenticated `userId`, `action`, `entityType`, `entityId`, optional JSON `oldValues` and `newValues`, UTC `timestamp`, and the HTTP request's `correlationId`. Event payloads are built explicitly from business fields. The writer rejects field names containing password, token, secret, hash, credential, key, or salt and accepts only scalar values. No request body, authentication token, password, or password hash is copied into an event.

The following successful API actions generate entries when they change business state:

| Action | Entity | Captured values |
| --- | --- | --- |
| `StockAdjusted` | ProductVariant | Location, quantity before/after, reason. |
| `PriceChanged` | ProductVariant | Price list, old/new price, currency. |
| `SaleCancelled` | Sale | Previous and cancelled status, sale number. |
| `GoodsReceived` | GoodsReceipt | Purchase order, location, line count, received quantity. |
| `InventoryCountCompleted` | InventoryCount | Location, status, line count, adjusted line count. |
| `UserDeactivated` | User | Active flag only. |
| `UserLoggedIn` | User | Successful password authentication method. |

Password login attempts, including failures, are retained separately in `identity.LoginAttempts`. See [Authentication and session lifecycle](../identity/AUTHENTICATION.md). A successful login writes `UserLoggedIn` only when Audit is enabled for the tenant.

`GET /audit/entries` requires `audit.view` permission. Optional filters are `userId`, `action`, `entityType`, `entityId`, `from`, and `to` (date-time with offset); `page` and `pageSize` default to 1 and 50, with page size capped at 100. Only the server can append entries. The Audit DbContext rejects modifications and deletions of existing entries. There is no audit update or delete endpoint.

Audit writes are awaited after the corresponding business operation succeeds. They currently use a separate database transaction, so an audit storage failure can leave a completed business operation without its audit entry. The API surfaces that failure. A future transactional outbox or shared transaction should close this consistency gap before relying on the trail for strict compliance.
