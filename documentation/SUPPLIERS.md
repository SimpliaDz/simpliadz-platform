# Suppliers (Purchasing)

Each tenant stores its suppliers in its own `purchasing.Suppliers` table. A supplier has a required name and optional phone, email, address, and notes. Deactivation sets `IsActive = false` and keeps the supplier row and ID. The API does not expose hard deletion, and the Purchasing DbContext rejects supplier deletes. [Purchase orders](PURCHASE_ORDERS.md) retain their `SupplierId` when a supplier is deactivated.

| Route | Permission | Purpose |
|---|---|---|
| `POST /suppliers` | `suppliers.manage` | Create a supplier. Name is required. |
| `PUT /suppliers/{id}` | `suppliers.manage` | Update name and contact details. |
| `PUT /suppliers/{id}/deactivate` | `suppliers.manage` | Deactivate without deleting history. |
| `GET /suppliers/{id}` | `suppliers.view` | Retrieve active or inactive supplier by ID. |
| `GET /suppliers?search=&isActive=&page=1&pageSize=50` | `suppliers.view` | Search name, phone, or email, optionally filter by active state, and paginate up to 100 per page. |

The routes require an authenticated user, the configured tenant, and the enabled Purchasing module. The migrator adds the Purchasing schema and seeds supplier permissions into the local Admin role. `Purchasing` is enabled for the `dev` and `demo` entries in the shared `src/tenants.json`. Run `dotnet run --project src/SimpliaDz.DatabaseMigrator -- --tenant dev` (or `demo`) before calling the routes. The supplier row version detects concurrent updates.
