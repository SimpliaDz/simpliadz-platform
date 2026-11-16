# Cash registers and sessions

Cash registers belong to a store location. A location may have several registers. Each register may have one open session at a time, and several cashiers may record payments into that session. Closing preserves the session; reopening creates a new one.

## Workflow

1. Create a register with `POST /cash-registers` using an active location, a code unique at that location, and a name.
2. Open a session with `POST /cash-sessions/open` (`cashRegisterId`, `openingCash`). The API records the opening user and time.
3. Record payments through `POST /sales/{saleId}/payments` with `cashSessionId`. When a location has active registers, a session is required. The session must be open and at the sale location. Several cashiers can use the same session.
4. Record a cash deposit or withdrawal using `POST /cash-sessions/{id}/movements` (`amountDelta`, `reason`). Positive amounts add cash; negative amounts remove it. Movements are append only.
5. Close with `POST /cash-sessions/{id}/close` (`countedCash`). Expected cash is opening cash plus cash payments plus cash movements. The response includes the counted cash and difference. Card and other payments appear separately and do not increase expected cash.

`GET /cash-sessions/current?locationId=...` lists all open sessions at a location. `GET /cash-sessions?locationId=...` returns history. `GET /cash-sessions/{id}/movements` returns cash movement history. Closed sessions cannot accept payments or movements and cannot be closed again. Register deactivation requires its session to be closed.

## Access and migration

The Sales module and tenant are required. `cash-registers.view`, `cash-registers.manage`, `cash-sessions.view`, and `cash-sessions.manage` permissions protect the new endpoints. The Sales migration `Sales_CashSessions` creates register, session, and movement tables and adds an optional session reference to existing payments. Existing payments remain intact. Run the database migrator for each tenant before using the endpoints.

Cash refunds do not yet create a payment reversal automatically. If cash leaves the register for a refund, record a negative cash movement with a reason.
