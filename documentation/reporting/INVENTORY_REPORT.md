# Inventory Report (SMP-49)

`GET /reports/inventory?locationId={optional-guid}&includeZeroStock=false&page=1&pageSize=50` returns current physical stock per product variant. It requires the current tenant, the Reporting and Inventory modules, and `reports.inventory.view` permission. The location filter may name an inactive location so historical stock remains visible. Without a location, quantities are summed across all locations. `includeZeroStock=true` includes catalog variants with no balance, even when a location is selected. Inactive products and variants remain visible for accounting of stock already held.

The response contains paginated `items` and summary totals calculated over **all matching variants**, not just the page. `totalQuantityOnHand` is the sum of `StockBalance.QuantityOnHand`; the report never changes balances or movements. `totalVariants` counts rows after the zero-stock filter.

For each variant, valuation uses the unit cost of its latest posted goods receipt, ordered by receipt time and receipt-line ID. This follows the same latest-receipt-cost convention as sale cost snapshots. It is an estimate, not FIFO or weighted-average inventory accounting. The selected cost applies to the variant's stock across all locations. When Purchasing is disabled or a variant has no receipt, its positive stock has unknown cost: `unitCost` and `stockValue` are null, its quantity contributes to `unvaluedQuantity`, and `totalStockValue` is null. `knownStockValue` still sums the priced rows. Zero-stock rows have stock value zero even without cost.

The report is read only and tenant isolated through the tenant database connection. Page size is 1–100. No new table or migration is needed.
