# Dashboard Summary (SMP-51)

`GET /reports/dashboard/today` returns a tenant-scoped, read-only snapshot for the current **UTC calendar date**. It requires the Reporting module and `reports.dashboard.view` permission. The response includes `date` and `timeZone: "UTC"` so clients can label the numbers accurately. Tenant-specific business time zones are not configured yet.

| Field | Definition |
| --- | --- |
| `revenue` | Completed sales revenue from the existing sales summary report. |
| `grossProfit` | Revenue minus the historical sale-line cost snapshots; expenses are not deducted. |
| `transactionCount` | Completed sales count. |
| `itemsSold` | Quantity sold on completed sale lines. |
| `averageBasket` | Revenue divided by transaction count; zero if there are no transactions. |
| `expenses` | Sum of expenses whose `ExpenseDate` is today. |
| `lowStockCount` | Number of active variant/location policy pairs at or below the reorder point. |

Sales figures follow [Sales Summary Report](SALES_SUMMARY_REPORT.md), including its handling of returns. Low-stock count follows [Low Stock Report](LOW_STOCK_REPORT.md). The dashboard composes their use-case contracts and an Accounting expense-total reader rather than querying their domain entities. The expense reader uses a database aggregate; the low-stock service returns only one row while its `totalItems` covers the full result set.

The response also includes `salesAvailable`, `accountingAvailable`, and `inventoryAvailable`. A disabled tenant module is not queried and its corresponding metrics are zero with its availability flag set to false. No stored dashboard state, migration, or background job is introduced.
