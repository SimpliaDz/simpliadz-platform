# Low Stock Report (SMP-50)

`GET /reports/low-stock?locationId={optional-guid}&page=1&pageSize=50` returns active product variants whose current physical `QuantityOnHand` is **at or below** the configured `InventoryPolicy.ReorderPoint` at an active location. The report requires the current tenant, Reporting and Inventory modules, and `reports.low-stock.view` permission.

Each item includes the variant and product IDs, current product name and SKU, location ID and name, `quantityOnHand`, `reorderPoint`, and `isOutOfStock`. A missing `StockBalance` counts as zero, so a policy at reorder point zero can still appear. Variants without a policy are excluded because no threshold has been configured. Inactive products, variants, and locations are excluded because this report is for actionable replenishment. An optional `locationId` narrows the report to one location.

`totalItems` counts all matching variant/location pairs before pagination; `items` contains the requested page. Page size is 1–100. The report reads current balances and policies without modifying stock or creating purchase orders. The older `/stock/policies?belowReorderPoint=true` filter still means strictly below; this report includes equality.
