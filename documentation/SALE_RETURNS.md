# Sale returns (SMP-43)

Sale returns are recorded against an existing completed sale. A return has a client-generated ID, a return number, an active location, a reason, the acting user, and one or more original sale lines. Each line records its quantity, variant, refund amount snapshot, and whether the item can be restocked. The return and its lines are immutable after processing.

## API

All routes require the current tenant and the Sales module. Creation requires both `sales.manage` and `sales.returns.manage`; reads require `sales.view`.

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/sales/{saleId}/returns` | Process a return atomically |
| GET | `/sales/{saleId}/returns` | List return history for a sale |
| GET | `/returns/{id}` | Retrieve a return |

Example request:

```json
{
  "id": "88888888-8888-4888-8888-888888888888",
  "locationId": "11111111-1111-4111-8111-111111111111",
  "reason": "Customer changed size",
  "lines": [
    {
      "originalSaleLineId": "22222222-2222-4222-8222-222222222222",
      "quantity": 1,
      "restock": true
    }
  ]
}
```

The caller supplies a new return ID for safe retries. Sending the same ID again returns a conflict and does not create a second return. A return line must belong to the original sale, its quantity must be positive, and the cumulative returned quantity cannot exceed the original sold quantity. A sale becomes `PartiallyReturned` until all sold quantities are returned, then `Returned`. `SaleLine.ReturnedQuantity` is exposed in sale reads.

Refund values use the original completed sale's price and discounts, never the current price list. The sale-level discount is allocated proportionally across lines; the effective unit refund is rounded down to four decimal places. This calculation records the amount due to the customer. Actual cash or card refund settlement is outside this ticket.

`restock: true` creates a positive `SaleReturn` stock movement and increments the location's stock balance. `restock: false` records a damaged or otherwise unsellable return without changing stock. Sale state, return history, and stock changes share one SQL transaction. An error in any write rolls back all of them. The operation checks the tenant's active Sales and Inventory setup and requires an active return location.

Apply the `Sales_Returns` migration before calling the routes. It adds `ReturnedQuantity` to sale lines and creates `SaleReturns` and `SaleReturnLines` in the tenant database's `sales` schema.
