# Expenses (SMP-45)

Expenses are tenant-scoped Accounting records. An expense has a required active category and positive decimal amount, a required business date, optional active location, optional description, creator ID, and creation/update timestamps. Amount uses `decimal(18,4)`; the expense date is stored as a SQL `date` without a time zone.

| Method | Route | Permission | Purpose |
| --- | --- | --- | --- |
| POST | `/expenses` | `expenses.manage` | Create an expense. |
| PUT | `/expenses/{id}` | `expenses.manage` | Correct category, location, amount, date, or description. |
| GET | `/expenses/{id}` | `expenses.view` | Retrieve one expense. |
| GET | `/expenses?fromDate=2026-11-01&toDate=2026-11-30&expenseCategoryId={id}&locationId={id}&page=1&pageSize=50` | `expenses.view` | Filter expenses; filters are optional and date bounds are inclusive. |

Example create/update body:

```json
{
  "expenseCategoryId": "11111111-1111-4111-8111-111111111111",
  "locationId": null,
  "amount": 1250.0000,
  "expenseDate": "2026-11-02",
  "description": "Office rent"
}
```

The API takes `CreatedBy` from the authenticated user; callers cannot change it. A new expense requires an active category. A supplied location must be active in Inventory, and the Inventory module must be enabled. Editing an expense can retain its current category or location after either is deactivated, preserving historical references, but switching to another reference requires it to be active. Expenses cannot be hard-deleted through the Accounting DbContext. Corrections use a row-version concurrency token. Separate audit events for corrections are not part of this ticket.

The `Accounting_Expenses` migration adds `accounting.Expenses` with a restrictive foreign key to `accounting.ExpenseCategories`, a positive-amount check, and indexes for date, category, and location filters. Location is validated through the API's Inventory adapter rather than a cross-module EF relationship. Run the migrator for each tenant after updating to this version.
