# Inventory Core

Stock is tracked per `ProductVariant` and `Location` within the tenant database. `StockBalance` stores the current physical `QuantityOnHand` and `UpdatedAt`. The unique `(VariantId, LocationId)` index prevents duplicate balances. The SQL table remains `inventory.StockItems` so existing stock data is preserved; the code uses the `StockBalance` name. The SMP-28 migration adds a SQL Server `rowversion` column.

An absent balance row means zero physical stock. A newly created variant does not need a balance row for every location. The variant total query returns zero when there are no rows. Balance rows are created by the first nonzero stock operation.

Every stock change uses the stock service. A receipt adds a positive quantity; an adjustment sets the physical quantity. The service checks that the variant and location are active, then updates the balance and inserts a `StockMovement` in one serializable transaction. A concurrent update or first-row race retries the full operation so the movement delta matches the committed balance. A no-op adjustment writes neither a balance change nor a movement. Negative physical stock is rejected. The demo seeder also inserts a matching receipt movement when it creates initial stock and never refills an existing balance.

| Route | Permission | Purpose |
|---|---|---|
| `GET /stock/variants/{variantId}` | `stock.view` | Current balance rows for one variant by location. |
| `GET /stock/variants/{variantId}/total` | `stock.view` | Total physical stock across all locations. |
| `GET /stock/locations/{locationId}` | `stock.view` | Current balance rows for one location by variant. |
| `POST /stock/receipts` | `stock.manage` | Receive stock with `productVariantId`, `locationId`, `quantity`, optional `reference` and `note`. |
| `POST /stock/adjustments` | `stock.manage` | Set physical stock with `productVariantId`, `locationId`, `quantityOnHand`, optional `note`. |

These routes require the Inventory module and a resolved tenant. There are no generic stock balance create, update, or delete endpoints. Stock movements remain the historical record of changes; balances are an optimized current-state snapshot.

Run the database migrator for each tenant after deploying this change. It adds the rowversion column and seeds the new `stock.view` and `stock.manage` permissions into the Admin role.
