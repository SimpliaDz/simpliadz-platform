# Stock Transfers

A stock transfer is a tenant-scoped Inventory business document. It starts in `Draft`, may contain several distinct product variants, and ends in `Completed` or `Cancelled`. A draft or cancelled transfer has no stock effect. The transfer number is generated from its ID and is unique within the tenant database.

| Route | Permission | Purpose |
|---|---|---|
| `POST /stock-transfers` | `stock.manage` | Create a draft with `sourceLocationId` and `destinationLocationId`. |
| `POST /stock-transfers/{id}/lines` | `stock.manage` | Add `{ "productVariantId": "...", "quantity": 3 }` to a draft. |
| `POST /stock-transfers/{id}/complete` | `stock.manage` | Complete the transfer once. |
| `POST /stock-transfers/{id}/cancel` | `stock.manage` | Cancel a draft without moving stock. |
| `GET /stock-transfers/{id}` | `stock.view` | Read a transfer and its lines. |
| `GET /stock-transfers?page=1&pageSize=50` | `stock.view` | List transfers, newest first; page size is at most 100. |

Source and destination must be different active locations. Every line needs an active variant and positive quantity, and a variant may appear only once in a document. Lines can be added only while the transfer is a draft. A completed or cancelled transfer cannot be completed or edited as a draft.

At completion the service checks stock for every line at the source. SimpliaWear currently prevents negative physical stock, so insufficient quantity rejects the entire transfer. It then subtracts each line from the source balance, adds it to the destination balance, and appends paired `TransferOut` and `TransferIn` movements. Each movement references the transfer ID and number and records the completing user. The status, balances, and movements are saved in one serializable transaction. A row version and revision on the document protect changes to its lines and status during concurrent requests.

Run the database migrator for each tenant to create `inventory.StockTransfers` and `inventory.StockTransferLines` before using these routes. The new tables have location and variant foreign keys, a unique transfer number, a unique variant per transfer, positive line quantity, and distinct-location constraints.
