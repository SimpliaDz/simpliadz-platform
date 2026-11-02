# Customers (SMP-38)

Customers belong to the current tenant's Sales database schema. A customer has a required name, optional phone, email, address, and notes, and a `Retail` or `Wholesale` type. The default type for a new customer is `Retail`. Deactivation preserves the customer ID and details; Sales rejects hard deletion so future sales transactions can retain their customer association. Sales transaction documents are not implemented yet.

| Route | Permission | Purpose |
|---|---|---|
| `POST /customers` | `customers.manage` | Create a customer. |
| `PUT /customers/{id}` | `customers.manage` | Update details and type. |
| `PUT /customers/{id}/deactivate` | `customers.manage` | Deactivate without deleting history. |
| `GET /customers/{id}` | `customers.view` | Retrieve an active or inactive customer. |
| `GET /customers?search=&customerType=&isActive=&page=1&pageSize=50` | `customers.view` | Search name or phone and filter by type or active state. |

The routes require an authenticated user, the selected tenant, and the enabled Sales module. Validation rejects blank names, unknown types, invalid email addresses, and oversized fields. Customer type is stored as a string; a row version detects concurrent updates. Name and phone are indexed for search. The API exposes no delete route, and the Sales DbContext rejects customer deletions.

`Sales` is enabled for the `dev` and `demo` entries in the shared `src/tenants.json`. Run `dotnet run --project src/SimpliaDz.DatabaseMigrator -- --tenant dev` (or `demo`) before calling these routes. This applies the first Sales migration and adds customer permissions to the local Admin role. The optional Sales SQL Server integration test creates isolated tenant databases when `SIMPLIADZ_INTEGRATION_CONNECTIONSTRING` is set.
