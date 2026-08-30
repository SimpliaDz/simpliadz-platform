# Business Locations

Business locations are part of the existing Inventory module. A separate module is unnecessary because stock items and movements already reference the `inventory.Locations` table.

## Data model

`InventoryLocation` stores:

- `Id`
- `Name` (required, maximum 150 characters)
- `Code` (optional, normalized to uppercase, maximum 50 characters)
- `Type` (`Store` or `Warehouse`)
- `Address` (optional, maximum 500 characters)
- `Phone` (optional, maximum 50 characters)
- `IsActive`
- `CreatedAt`
- `UpdatedAt`

The unique filtered index on `Code` is scoped naturally by the tenant-per-database model. Location names do not need to be unique.

## HTTP API

All routes require an authenticated, tenant-bound token, the `X-Tenant-Id` header, and the Inventory module enabled for that tenant.

| Method | Route | Permission | Behavior |
| --- | --- | --- | --- |
| `GET` | `/locations` | `locations.view` | Lists active and inactive locations. |
| `GET` | `/locations/{id}` | `locations.view` | Retrieves one location or returns 404. |
| `POST` | `/locations` | `locations.create` | Creates an active location. |
| `PUT` | `/locations/{id}` | `locations.edit` | Updates descriptive fields without changing history. |
| `PUT` | `/locations/{id}/deactivate` | `locations.edit` | Marks a location inactive; it does not delete it. |

Example create/update body:

```json
{
  "name": "Algiers Central Store",
  "code": "ALG-01",
  "type": "Store",
  "address": "Algiers",
  "phone": "+213 555 00 00 00"
}
```

Duplicate codes return HTTP 409. Invalid names, types, or field lengths return HTTP 400 validation details.

## Historical data and stock rules

There is deliberately no delete endpoint. Existing `StockItems` and `StockMovements` keep restrictive foreign keys to locations, and listing includes inactive locations so historical data remains interpretable.

`StockService.ReceiveAsync` and `StockService.AdjustToAsync` require both an active location and an active product variant. Unknown or inactive identifiers are rejected before stock or ledger records are changed.

## Permissions

Permission strings are constants under `SharedKernel/Authorization/PermissionNames.cs`. Its catalog is the single source for permission names, display labels, categories, policy registration, and Identity persistence seeding.

## Migration

`Inventory_BusinessLocations` evolves the existing `inventory.Locations` table in place. Existing locations are retained, assigned the `Store` type, and receive a migration-time UTC creation timestamp. The migration also changes the location-name index from unique to non-unique while retaining unique codes.
