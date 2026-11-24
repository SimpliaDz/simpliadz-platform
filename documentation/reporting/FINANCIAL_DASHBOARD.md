# Financial dashboard

`GET /reports/financial-dashboard?fromDate=2026-11-01&toDate=2026-11-30&locationId={optional-guid}` provides a date-range dashboard for the current tenant. It requires the Reporting and Sales modules and the `reports.sales-summary.view` permission.

The optional `locationId` restricts all sales, returns, payments, and expenses to one shop. Without it, the dashboard represents the entire organisation and compares sales locations.

## Measures

| Measure | Source and meaning |
| --- | --- |
| Net sales | Completed-sale total less refunds recorded in the selected dates. |
| Discounts | Difference between recorded sale subtotal and total; includes line and sale discounts. |
| Cost of goods sold | Unit-cost snapshots for sold lines less the cost snapshots of returned lines. |
| Gross profit / margin | Net sales less COGS, and gross profit divided by net sales. |
| Expenses | Recorded Accounting expenses dated in the period (zero when Accounting is disabled). |
| Operating contribution | Gross profit less recorded expenses. This is not statutory net income: taxes, payroll and unrecorded costs may be absent. |
| Payment mix | Recorded sale payments, grouped by payment method. |
| Top products | Highest line revenue products. Sale-level discounts are represented in the headline net-sales total, not redistributed among products. |

Daily trend points include sales/returns and expense dates. Sales are completed-sale dates and all dates are UTC until tenant time-zone support is added.

The dashboard intentionally does not show cash-flow forecasts, customer acquisition cost, retention, tax liability, or inventory turns. The current data model does not record enough reliable data for those measures.
