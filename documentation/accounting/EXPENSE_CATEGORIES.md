# Expense Categories (SMP-44)

Expense categories are maintained by the tenant's Accounting module. A category has an ID, required name, active flag, and creation/update timestamps. Names are trimmed, limited to 150 characters, and unique across active and inactive categories so historical reports cannot confuse two categories with the same name.

| Method | Route | Permission | Purpose |
| --- | --- | --- | --- |
| GET | `/expense-categories?isActive=true` | `expense-categories.view` | List categories; omit `isActive` for all. |
| GET | `/expense-categories/{id}` | `expense-categories.view` | Retrieve one category. |
| POST | `/expense-categories` | `expense-categories.manage` | Create with `{ "name": "Rent" }`. |
| PUT | `/expense-categories/{id}` | `expense-categories.manage` | Rename with `{ "name": "Utilities" }`. |
| PUT | `/expense-categories/{id}/deactivate` | `expense-categories.manage` | Deactivate without deleting. |

Routes are tenant-scoped and require the Accounting module to be enabled. The local `dev` and `demo` tenants enable it; run the database migrator for each tenant after updating to this version. Each tenant's Accounting DbContext uses its own configured database. The `Accounting_ExpenseCategories` migration creates `accounting.ExpenseCategories` with a unique name index and row-version concurrency token.

Hard deletion is blocked by the Accounting DbContext. Deactivated categories retain their IDs and remain readable. SMP-45 adds `Expense` with a restrictive category foreign key, so existing expenses keep their category reference after deactivation. New expenses must use an active category.
