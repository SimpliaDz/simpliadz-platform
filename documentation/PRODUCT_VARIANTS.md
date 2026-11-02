# Product Variants

A `ProductVariant` is the sellable and stockable unit of a product. Stock items and movements already reference `VariantId` and a location; the commercial `Product` has no stock quantity.

New variants select one or more generic attribute values from the Inventory attribute catalog. Each selected value belongs to exactly one attribute, and a variant may select at most one value for a given attribute. A sorted hash of the chosen value IDs forms the combination key. The database enforces uniqueness of `(ProductId, CombinationKey)`, so Black/M and M/Black are the same combination for one product. Existing variants retain their nullable combination key and legacy Size/Color text fields for compatibility.

SKU is required and unique across the tenant database. Barcode is optional and unique when present. Existing unique indexes enforce both rules. Variants retain retail and optional wholesale prices. Deactivation changes `IsActive` without deleting stock or movement history.

| Route | Permission | Purpose |
|---|---|---|
| `POST /variants/products/{productId}` | `variants.create` | Create a variant. |
| `GET /variants/{id}` | `variants.view` | Retrieve by ID. |
| `GET /variants/by-sku/{sku}` | `variants.view` | Find by SKU. |
| `GET /variants/by-barcode/{barcode}` | `variants.view` | Find by barcode. |
| `PUT /variants/{id}/deactivate` | `variants.edit` | Deactivate while preserving history. |

Create body:

```json
{
  "sku": "TS-BLK-M",
  "barcode": "12345",
  "retailPrice": 1500,
  "wholesalePrice": 900,
  "attributeValueIds": ["11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"]
}
```

The migration adds `VariantAttributeValues` and a nullable `CombinationKey` to `ProductVariants`. Apply it per tenant with the database migrator. Variant generation, pricing by attribute, and changes to existing legacy variant values are separate work.
