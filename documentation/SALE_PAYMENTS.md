# Sale Payments (SMP-42)

Payments are posted against a **Completed** sale. Each payment has a caller-supplied `id` GUID for safe retries, a positive decimal amount, a `Cash`, `Card`, `BankTransfer`, or `Other` method, a server timestamp, and the authenticated receiving user. Reusing an ID is rejected. Payments are immutable and cannot be deleted through the Sales DbContext.

| Route | Permission | Purpose |
|---|---|---|
| `POST /sales/{saleId}/payments` | `sales.manage` + `sales.payments.manage` | Record `{ "id": "...", "amount": 1000, "paymentMethod": "Cash" }`. |
| `GET /sales/{saleId}/payments` | `sales.view` | List a sale's payment history. |
| `GET /payments/{id}` | `sales.view` | Retrieve one payment. |

The sale stores `PaidAmount` and exposes `BalanceDue = Total - PaidAmount`. Partial payments are supported. A new payment cannot exceed the remaining balance; this V1 rule rejects overpayments and does not model change given to the customer. A zero-total completed sale has no balance due and needs no payment. Payment insertion and the sale's paid-total update happen in one serializable Sales database transaction. A failure rolls back both. A DbContext guard rejects direct paid-total changes without matching new payments.

Run the tenant migrator to apply `Sales_Payments` and seed the payment permission. The optional SQL Server integration test covers partial and full payment, duplicate ID, overpayment, and rollback after a forced payment insert failure. Payment gateways and complex reconciliation remain out of scope.
