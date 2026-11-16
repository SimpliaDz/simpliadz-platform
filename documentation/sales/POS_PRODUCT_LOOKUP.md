# POS Product Lookup (SMP-40)

POS lookup is a tenant-scoped Sales API backed by Inventory product, variant, price list, and stock tables. It returns one row per active, priced variant: product ID and name, variant ID, SKU, barcode, color, size, selected-list price and currency, and optional current quantity at a POS location. The optional quantity is omitted when no location is supplied or no balance row exists. A missing balance represents zero physical stock for selling decisions.

| Route | Purpose |
|---|---|
| `GET /pos/products/by-barcode/{barcode}?priceListId=...&locationId=...` | Exact barcode lookup. |
| `GET /pos/products/by-sku/{sku}?priceListId=...&locationId=...` | Exact SKU lookup. |
| `GET /pos/products?search=Classic&priceListId=...&locationId=...&page=1&pageSize=25` | Product-name **prefix** search, returning variants. Page size is at most 50. |

All routes require `sales.manage`, the Sales module, and an active price list. If supplied, the location must be active. The Inventory module must also be enabled. Inactive products, inactive variants, inactive price lists, and variants without a price in the selected list are excluded. The lookup does not reserve stock; sale completion checks the balance again before deduction.

Barcode and SKU lookup use existing unique indexes. Name search runs in SQL with a bounded page and a new index on `inventory.Products.Name`. Prefix matching is deliberate so the index can support frequent POS queries. For example, `Classic` matches `Classic Cotton T-Shirt`; `Cotton` alone does not. The API does not load the full catalog into memory. Run the database migrator for each configured tenant to apply `Inventory_PosProductNameIndex` before using the new search route.
