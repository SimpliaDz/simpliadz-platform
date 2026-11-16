# Opening Stock

Use `POST /stock/opening` to initialize physical stock for one product variant at one location. It requires `stock.manage` and an authenticated user. Example body:

```json
{
  "productVariantId": "00000000-0000-0000-0000-000000000001",
  "locationId": "00000000-0000-0000-0000-000000000002",
  "quantity": 12,
  "reason": "Counted during onboarding"
}
```

The variant and location must be active. Quantity can be zero or greater. The response includes the new balance and the opening movement ID, timestamp, and actor. `GET /stock/movements` exposes the event in the audit history.

Opening stock is one-time per variant/location. If a balance already exists or any movement history has started, the operation returns a conflict; use a receipt or a reasoned adjustment for later changes. The balance and `OpeningBalance` movement are committed together. A zero opening creates a zero balance row and a zero-delta opening movement to record that initialization occurred. Other movement types still require a nonzero delta.

The SMP-30 migration changes the ledger check constraint to permit zero only for `OpeningBalance` and adds a filtered unique index that permits at most one opening event per variant/location. Run the database migrator for each tenant before using the endpoint.
