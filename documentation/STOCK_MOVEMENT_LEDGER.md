# Stock Movement Ledger

`StockMovement` is the append-oriented history of physical stock changes. Each row records the variant, location, signed quantity delta, movement type, timestamp, actor, optional reason, and optional originating document (`ReferenceType` plus `ReferenceId`). A positive delta increases stock; a negative delta decreases it. A document type and ID must be supplied together for new movements. `Reference` remains an optional human-readable identifier.

`OpeningBalance` can have a zero delta to record an explicit zero opening count. All other movement types require a nonzero delta.

The types supported by the domain are `OpeningBalance`, `PurchaseReceipt`, `Sale`, `SaleReturn`, `Adjustment`, `TransferOut`, and `TransferIn`. Historical `Receipt`, `Issue`, and `ReturnIn` values remain readable with their original numeric values. The demo seed now uses `OpeningBalance`; the receipt endpoint uses `PurchaseReceipt`; adjustments use `Adjustment`. Sale and transfer operations can use the corresponding types when those workflows are built.

The existing ledger table and columns remain in place. `MovementType` maps to the existing `Type` column, `Reason` maps to `Note`, and `ReferenceType`/`ReferenceId` map to `SourceDocumentType`/`SourceDocumentId`. The SMP-29 migration adds `CreatedBy` and a nonzero delta constraint. Old rows receive `CreatedBy = "legacy"`, since their actual actor cannot be recovered. Seed rows use `"system"`; authenticated API changes store the user's ID. An EF save guard rejects normal updates and deletes of movements, deletes of balances, and balance changes without a matching new movement. There are no ledger update or delete API routes.

| Route | Permission | Purpose |
|---|---|---|
| `GET /stock/movements?productVariantId=&locationId=&page=1&pageSize=50` | `stock.view` | Query history, newest first, with optional variant and location filters and a maximum page size of 100. |
| `POST /stock/receipts` | `stock.manage` | Add a purchase receipt. Supports `referenceType`, `referenceId`, and `reason` in addition to the existing fields. |
| `POST /stock/adjustments` | `stock.manage` | Set physical stock. A reason is required. |

The receipt and adjustment services save the balance and movement in one transaction. No-op adjustments write neither. `StockBalance` is the current snapshot; movements are the historical explanation. Run the database migrator for each tenant before using the new ledger fields.
