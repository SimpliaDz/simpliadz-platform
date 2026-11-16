# Brands

Brands are managed within the Inventory module and belong to the tenant database selected for the request. A brand has an ID, required name (up to 150 characters), active state, and an update timestamp.

The existing unique name index remains in force for all brands, including inactive ones. Names are trimmed on create and update. A duplicate name returns `409 Conflict`, and an empty or overlong name returns `400 ValidationError`. An unknown ID returns `404 NotFound`.

| Route | Permission | Purpose |
|---|---|---|
| `GET /brands` | `brands.view` | List active and inactive brands. |
| `GET /brands/{id}` | `brands.view` | Retrieve one brand. |
| `POST /brands` | `brands.create` | Create a brand using `{ "name": "Acme" }`. |
| `PUT /brands/{id}` | `brands.edit` | Rename a brand using the same body. |
| `PUT /brands/{id}/deactivate` | `brands.edit` | Mark it inactive without deleting products. |

Products reference a brand by `BrandId`. New assignments require an active brand. Deactivation preserves existing product references, and a product may retain its current inactive brand during an update. The brand foreign key already restricts deletion, so this feature needs no database migration.
