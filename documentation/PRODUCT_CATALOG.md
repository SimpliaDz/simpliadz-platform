# Product Catalog

## Purpose

The Inventory product catalog separates the commercial definition of a product from the items that are sold and stocked.

- `Product` describes an item such as **Classic Cotton T-Shirt**.
- `ProductVariant` describes a sellable/stockable choice such as **Black / M**, identified by a SKU and carrying its prices.
- `StockItem` stores quantity for one variant at one business location.

This separation is required for clothing and keeps the catalog independent from inventory balances.

## Domain model

### Product

| Property | Purpose |
|---|---|
| `Id` | Stable product identity. |
| `Name` | Required commercial name, represented by the `ProductName` value object. |
| `Code` | Optional internal code, normalized to uppercase and unique when present. |
| `Description` | Optional description, maximum 2,000 characters. |
| `BrandId` | Optional reference to an active brand. |
| `ProductCategoryId` | Optional internal reference to an active category; exposed as `CategoryId` by the API. |
| `IsActive` | Controls whether the product remains available for normal use. |
| `CreatedAt` / `UpdatedAt` | Audit timestamps. |
| `Variants` | The product's sellable/stockable variants. |

There is no quantity, quantity-on-hand, or location field on `Product`.

### ProductVariant

A variant belongs to exactly one product and stores SKU, optional barcode/color/size, retail and wholesale prices, and active state. The Inventory schema makes SKU unique globally inside the tenant database.

### Stock

Quantity belongs to `StockItem`, keyed by `VariantId + LocationId`. Stock movements also reference the variant and location. Therefore one variant can have independent balances in the main store and a warehouse without duplicating the product catalog definition.

## Business rules

- Product name is required and is limited to 200 characters.
- Product code is optional, normalized to uppercase, limited to 50 characters, and unique inside the current tenant database.
- Description is optional and limited to 2,000 characters.
- A newly supplied brand or category must exist and be active. An existing product may retain its current category after that category is deactivated.
- Deactivation changes `IsActive` to `false`; it does not remove the row.
- The application exposes no hard-delete command or endpoint.
- Product-to-variant deletion uses `Restrict`, while stock rows and movements also restrict variant deletion. This prevents cascading removal of transaction or stock history.
- Tenant isolation follows the platform's database-per-tenant model: the tenant context selects the connection string before `InventoryDbContext` is created.

## HTTP API

All routes require the Inventory module to be enabled for the resolved tenant and require an authenticated user with the indicated permission.

| Method and route | Permission | Behavior |
|---|---|---|
| `GET /products?search=&isActive=` | `products.view` | Lists products, optionally filtering by text and active state. Search checks name, code, and description. |
| `GET /products/{id}` | `products.view` | Retrieves one product and its variants. |
| `POST /products` | `products.create` | Creates a product. |
| `PUT /products/{id}` | `products.edit` | Updates the commercial product definition. |
| `PUT /products/{id}/deactivate` | `products.edit` | Deactivates without deleting history. |

Create and update body:

```json
{
  "name": "Classic Cotton T-Shirt",
  "code": "TSHIRT-CLASSIC",
  "description": "100% cotton",
  "brandId": null,
  "categoryId": null
}
```

The product response includes product metadata and variant descriptions/prices. It intentionally contains no inventory quantity. Stock balances require a separate location-aware stock workflow.

## Errors

- `400 ValidationError` for missing name or maximum-length violations.
- `404 NotFound` when a product does not exist or a supplied brand/category is missing or inactive.
- `409 Conflict` when another product already uses the supplied code.
- `401/403` when authentication or the required permission is missing.

## Persistence migration

`Inventory_ProductCatalogRefactor` makes the filtered product-code index unique and changes the Product-to-ProductVariant foreign key from cascade deletion to restrictive deletion. Run the tenant-aware database migrator so the migration is applied independently to every configured tenant database.

## Tests

`tests/SimpliaDz.Inventory.Tests/ProductTests.cs` covers the no-stock-on-product rule, validation, code normalization, brand/category reference validation, update identity preservation, retrieval/search, and history-preserving deactivation.

Hierarchical category management is documented separately in [Product Categories](PRODUCT_CATEGORIES.md).
Brand management is documented in [Brands](BRANDS.md).
The generic Size, Color, and other attribute catalog is documented in [Product Attributes](PRODUCT_ATTRIBUTES.md).
Creation and lookup of sellable variants is documented in [Product Variants](PRODUCT_VARIANTS.md).
