# Product Performance Report (SMP-48)

Product performance is a tenant-scoped Reporting view over the Sales and Inventory modules. Both modules must be enabled. Date parameters use inclusive UTC calendar dates. The endpoint requires `reports.product-performance.view` permission.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/reports/products/performance?fromDate=2026-11-01&toDate=2026-11-30&rankBy=Quantity&page=1&pageSize=50` | Rank products by net quantity sold. Use `rankBy=Revenue` for net revenue. |
| GET | `/reports/products/{productId}/variants?fromDate=2026-11-01&toDate=2026-11-30&page=1&pageSize=50` | Retrieve variant performance, including variants with zero activity. |

Each row reports gross sold quantity and revenue, returned quantity and refund amount, and net quantity and revenue. Completed sales, including those now marked partially or fully returned, contribute on their original `SaleDate`. Processed returns contribute on `ReturnDate`, even if the original sale was outside the selected period. Net values can therefore be negative for a period containing more returns than sales. A returned item's `Restock` flag does not change report arithmetic: every accepted return reduces net sales.

Sales revenue comes from the completed sale's stored `Total` and line totals, never current list prices. Sale-level discounts are allocated proportionally over lines at four-decimal precision; the rounding remainder is assigned to one line so variant gross revenue sums to each sale's stored total. Refund values come from the immutable `SaleReturnLine.RefundAmount` snapshot. Product names, SKUs, and barcodes are current catalog labels; sales and return numbers use historical snapshots.

Ranking uses net quantity or net revenue, with product ID as a stable tie-breaker. Product rankings include products with sales or returns in the period. Variant detail includes all variants of the selected product, including inactive variants and those with no activity. Both endpoints are paginated (page size 1–100). No report table or background materialization is created.
