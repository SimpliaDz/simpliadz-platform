# Product Attributes

Inventory now has a generic `ProductAttribute` catalog and ordered `AttributeValue` records. For example, `Color` can contain Black, White, and Blue, while `Size` can contain S, M, L, and XL. The tenant-aware migrator seeds those examples idempotently.

Each value belongs to exactly one attribute through a required foreign key. Attribute names are unique in the tenant database, and values are unique within their attribute. Values have a nonnegative `DisplayOrder`; equal positions sort by value. Names and values are trimmed and limited to 150 characters. SQL unique indexes also protect concurrent creates.

| Route | Permission | Purpose |
|---|---|---|
| `GET /attributes` | `attributes.view` | List attributes with ordered values. |
| `GET /attributes/{id}` | `attributes.view` | Retrieve one attribute. |
| `POST /attributes` | `attributes.manage` | Create an attribute with `{ "name": "Material" }`. |
| `POST /attributes/{id}/values` | `attributes.manage` | Add `{ "value": "Cotton", "displayOrder": 0 }`. |
| `PUT /attributes/{id}/values/{valueId}/order` | `attributes.manage` | Change `{ "displayOrder": 1 }`. |

The existing ProductVariant `Size` and `Color` text fields remain in place for compatibility with seeded variants and stock history. Newly created variants can select values from this generic catalog through the [Product Variants](PRODUCT_VARIANTS.md) workflow. Attribute-based pricing and automatic variant generation remain outside this feature.
