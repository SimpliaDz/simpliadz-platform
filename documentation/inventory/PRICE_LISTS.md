# Price Lists

Inventory now supports tenant-specific price lists. A list has a name, three-letter currency code, type (`Retail` or `Wholesale`), and active state. A `PriceListItem` assigns a decimal price to one `ProductVariant`; its composite key prevents duplicate entries for the same variant in the same list. Prices must be nonnegative, enforced in the domain, validation, and a database check constraint.

| Route | Permission | Purpose |
|---|---|---|
| `GET /price-lists` | `price-lists.view` | List price lists. |
| `POST /price-lists` | `price-lists.manage` | Create a list, e.g. `{ "name": "Retail", "currencyCode": "DZD", "type": "Retail" }`. |
| `PUT /price-lists/{id}/variants/{variantId}` | `price-lists.manage` | Assign or replace `{ "price": 1500.25 }`. |
| `GET /price-lists/{id}/variants/{variantId}` | `price-lists.view` | Resolve a variant price and currency. |

The migration creates default DZD Retail and Wholesale lists and copies existing variant prices into them. The Inventory seed process fills missing entries for newly seeded demo variants without overwriting prices already managed in a list. Existing `RetailPrice` and `WholesalePrice` columns on ProductVariant are retained temporarily for compatibility; price list queries resolve only from `PriceListItems`. Promotions, customer pricing, quantity tiers, and timed pricing are separate features.
