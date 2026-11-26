# Purchase Orders (SMP-36)

Purchase orders live in each tenant's `purchasing` schema. A purchase order records an intended purchase from one supplier to one destination location. Its lines refer to Inventory product variants, with positive ordered quantities and nonnegative `decimal(18,4)` unit costs. The total is the sum of ordered quantity times unit cost. Order numbers are generated as unique `PO-<GUID>` values.

The API validates that the supplier, destination location, and product variants are active when they are used. Cross-module location and variant lookups are composed in the API host, so the Purchasing projects do not depend on Inventory projects. Purchasing and Inventory must both be enabled for a tenant to create or place an order.

| Route | Permission | Purpose |
|---|---|---|
| `POST /purchase-orders` | `purchase-orders.manage` | Create a draft with supplier, destination location, and optional notes. |
| `POST /purchase-orders/{id}/lines` | `purchase-orders.manage` | Add a variant, ordered quantity, and unit cost to a draft. A variant appears once per order. |
| `PUT /purchase-orders/{id}/notes` | `purchase-orders.manage` | Change draft notes. |
| `POST /purchase-orders/{id}/order` | `purchase-orders.manage` | Place a draft containing at least one line. |
| `POST /purchase-orders/{id}/cancel` | `purchase-orders.manage` | Cancel a draft or unreceived ordered PO. |
| `GET /purchase-orders/{id}` | `purchase-orders.view` | Retrieve an order with lines, totals, and received quantities. |
| `GET /purchase-orders?supplierId=&status=&page=1&pageSize=50` | `purchase-orders.view` | List and filter orders. |

Draft and Ordered status changes only touch Purchasing tables. They do not mutate stock. `PurchaseOrderLine.ReceivedQuantity` starts at zero. [Goods receipts](GOODS_RECEIPTS.md) advance an Ordered PO to PartiallyReceived or Received as lines are received, while adding stock movements and balances in one transaction. They reject over-receipts and cancelled POs.

The migration creates `purchasing.PurchaseOrders` and `purchasing.PurchaseOrderLines`, with a restrictive supplier foreign key, unique order number, unique variant per PO, SQL checks for quantities and cost, and a row version for concurrent edits. Run `.\scripts\migrate-tenant.ps1 -TenantId dev` from `simplia-wear-runner` (or choose another configured tenant) to apply it and seed the new permissions into the Admin role.
