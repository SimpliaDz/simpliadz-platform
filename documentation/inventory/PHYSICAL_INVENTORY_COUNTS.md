# Physical Inventory Counts (SMP-34)

A physical inventory count is a tenant-scoped Inventory document for one location. Starting a count snapshots every existing `StockBalance` at that location into `InventoryCountLine.ExpectedQuantity`. You can add an active variant that had no balance; its expected quantity is captured when it is added, normally zero. Physical quantities are entered per variant. `Difference` is calculated as `CountedQuantity - ExpectedQuantity` and is null until the line is counted.

| Route | Permission | Purpose |
|---|---|---|
| `POST /inventory-counts` | `stock.manage` | Start a draft for `{ "locationId": "..." }` and capture expected quantities. |
| `POST /inventory-counts/{id}/lines` | `stock.manage` | Add `{ "productVariantId": "..." }` to a draft, capturing its current expected quantity. |
| `PUT /inventory-counts/{id}/lines/{productVariantId}/count` | `stock.manage` | Enter `{ "countedQuantity": 15 }` for a draft line. |
| `POST /inventory-counts/{id}/complete` | `stock.manage` | Reconcile once; every line must have a physical quantity. |
| `POST /inventory-counts/{id}/cancel` | `stock.manage` | Cancel a draft without changing stock. |
| `GET /inventory-counts/{id}` | `stock.view` | Retrieve the document and its lines. |
| `GET /inventory-counts?locationId=&page=1&pageSize=50` | `stock.view` | List count history, optionally filtered by location. |

At completion, the service checks that the location and variants are still active and that current balances match the captured expected quantities. A changed balance or a new uncounted balance causes a conflict, so the user starts a fresh count instead of overwriting intervening stock activity. The service creates an `Adjustment` StockMovement for every nonzero difference, using the count ID as its reference, and applies the same delta to `StockBalance`. Zero differences create no movement. Status, balances, and movements are saved in one serializable SQL Server transaction. Completed or cancelled counts cannot be changed or completed again, and count history cannot be deleted through the Inventory DbContext.

The `Inventory_PhysicalCounts` migration creates `inventory.InventoryCounts` and `inventory.InventoryCountLines` with location and variant foreign keys, a unique variant per count, nonnegative quantity checks, and a row version for concurrent edits. Run the migrator for each configured tenant before using these routes. `InventoryCountIntegrationTests` uses an isolated SQL Server database when `SIMPLIAWEAR_INTEGRATION_CONNECTIONSTRING` is set; it verifies reconciliation, stale snapshot rejection, and rollback after a forced movement write failure.
