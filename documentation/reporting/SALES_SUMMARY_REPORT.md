# Sales Summary Report (SMP-47)

`GET /reports/sales/summary?fromDate=2026-11-01&toDate=2026-11-30` returns a date-range summary for the current tenant. Both dates are required and inclusive. The report uses UTC calendar dates because tenant time zones are not configured yet. The endpoint requires the Reporting module, the Sales module, and `reports.sales-summary.view` permission. Reporting is enabled for the local `dev` and `demo` tenants.

The response contains:

| Field | Calculation |
| --- | --- |
| `revenue` | Sum of the completed sales' stored `Total`, after line and sale discounts. |
| `costOfGoodsSold` | Sum of each sold line's stored `UnitCostSnapshot × Quantity`. |
| `grossProfit` | Revenue minus cost of goods sold. |
| `transactionCount` | Number of completed sale documents in the range. |
| `itemsSold` | Sum of their sold line quantities. |
| `averageTransactionValue` | Revenue divided by transaction count, rounded to four decimal places; zero if there are no transactions. |

`Completed`, `PartiallyReturned`, and `Returned` sales are counted by their original `SaleDate`. Draft and cancelled sales are excluded. This is an original-sales summary: returns do not alter the original sale date's revenue or cost here. A net-sales or return-period report can be added separately when its accounting rules are defined. The query reads historical sale total and unit cost snapshots from the tenant's Sales tables; it never reads current prices or product costs.

No Reporting table or migration is required for this read-only report. The API host implements the query because it composes the Reporting endpoint with Sales-owned data. SQL aggregates the results in one query, so empty date ranges return zero values without loading sale rows into application memory.
