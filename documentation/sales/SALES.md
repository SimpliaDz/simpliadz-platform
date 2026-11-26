# Sale Aggregate (SMP-39)

A sale is a tenant-scoped Sales document. A draft belongs to an active location and price list and may optionally reference an active customer. The authenticated user becomes its cashier. Lines are added by **SKU or barcode**, one variant per sale, with a positive quantity. The server resolves `UnitPrice` from the selected price list and stores it on the line. `UnitCostSnapshot` comes from the latest posted goods receipt for that variant when Purchasing is enabled; it is zero when no receipt cost is available. Changing a price list or later purchase cost does not rewrite existing sale lines.

`Subtotal` is the sum of quantity times unit price before discounts. Each line stores its own discount and line total; the sale can also have an order-level `DiscountAmount`. `Total` is the sum of line totals minus the order discount. All amounts are calculated on the server. [Sale Discounts](SALE_DISCOUNTS.md) adds fixed and percentage types with a separate permission. A draft has no sale number. Completing it assigns a unique `SALE-<GUID>` number and changes status to `Completed`. `PartiallyReturned` and `Returned` are reserved for a future return workflow; SMP-39 exposes no return operation.

| Route | Permission | Purpose |
|---|---|---|
| `POST /sales` | `sales.manage` | Create a draft with `locationId`, optional `customerId`, and `priceListId`. |
| `POST /sales/{id}/lines` | `sales.manage` | Add `sku` or `barcode`, `quantity`, and optional `discountAmount`. |
| `PUT /sales/{id}/discount` | `sales.manage` + `sales.discounts.apply` | Set a fixed or percentage order discount. |
| `POST /sales/{id}/complete` | `sales.manage` | Complete once and deduct inventory. |
| `POST /sales/{id}/cancel` | `sales.manage` | Cancel a draft without stock effects. |
| `GET /sales/{id}` | `sales.view` | Retrieve a sale with line snapshots and totals. |
| `GET /sales?locationId=&customerId=&status=&page=1&pageSize=50` | `sales.view` | List sale history. |

Completion checks that the location and every variant are still active and that each location/variant balance has enough physical stock. The current stock policy prevents negative balances. The API host composes Sales and Inventory DbContexts on **one SQL Server connection and serializable transaction**. It saves the completed sale together with one negative `Sale` StockMovement per line and the matching StockBalance deductions, then commits once. A failure rolls back all changes. Completing the same sale again is rejected. Completed sales cannot be edited through the API or deleted through the Sales DbContext. The customer foreign key uses restrictive deletion so historical sales remain linked when a customer is deactivated.

[Sale Payments](SALE_PAYMENTS.md) records partial or full payment against completed sales and exposes paid amount and balance due. Payments are immutable and posted atomically with the paid-total update.

Run `.\scripts\migrate-tenant.ps1 -TenantId dev` from `simplia-wear-runner` (or choose another configured tenant) to apply the `Sales_Sales` migration and seed `sales.view` / `sales.manage` permissions. The optional `SaleIntegrationTests` creates a temporary SQL Server database when `SIMPLIAWEAR_INTEGRATION_CONNECTIONSTRING` is set and verifies snapshots, stock deduction, insufficient stock rejection, duplicate completion, and rollback after a forced stock write failure.
