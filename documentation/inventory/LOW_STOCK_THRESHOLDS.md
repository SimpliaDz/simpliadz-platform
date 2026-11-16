# Low Stock Thresholds (SMP-46)

`InventoryPolicy` configures a reorder point and optional target stock level for one ProductVariant at one Location. It is separate from `StockBalance`: changing a threshold never edits physical stock, creates a movement, or creates a purchase order. Target stock level, when supplied, must be at least the reorder point.

| Method | Route | Permission | Purpose |
| --- | --- | --- | --- |
| PUT | `/stock/policies/{productVariantId}/{locationId}` | `stock.manage` | Create or update `{ "reorderPoint": 5, "targetStockLevel": 12 }`. Use `null` for no target. |
| GET | `/stock/policies/{productVariantId}/{locationId}` | `stock.view` | Retrieve one policy with current stock status. |
| GET | `/stock/policies?locationId={id}&belowReorderPoint=true&outOfStock=true&page=1&pageSize=50` | `stock.view` | Query configured policies and stock status. All filters are optional; supplied status filters combine with AND. |
| GET | `/stock/out-of-stock?locationId={id}&page=1&pageSize=50` | `stock.view` | Find all active variants with zero stock at one active location, including variants without a policy or balance. |

`QuantityOnHand` is read from the variant/location stock balance. A configured pair with no balance has quantity zero. `IsBelowReorderPoint` means quantity is strictly less than the reorder point. `IsOutOfStock` means quantity is zero, including when the reorder point is zero. The policy list covers configured pairs on active variants, products, and locations; a single policy remains readable even if one of those records is later deactivated. The separate out-of-stock route also finds unconfigured variants.

The `Inventory_Policies` migration creates `inventory.InventoryPolicies` with a composite key, restrictive variant/location foreign keys, a row-version concurrency token, and nonnegative threshold constraints. It copies any non-null legacy `StockItems.ReorderPoint` values into policies before dropping that old column. Run the migrator for each configured tenant after updating.
