# Goods Receipts (SMP-37)

A goods receipt records merchandise physically received for an Ordered or PartiallyReceived purchase order. It uses that order's destination location and copies each received line's variant and unit cost from the purchase order. The caller supplies an `id` GUID as a retry key; reusing that ID returns a conflict and cannot post stock twice.

| Route | Permission | Purpose |
|---|---|---|
| `POST /goods-receipts` | `goods-receipts.manage` | Post receipt lines to a PO and increase inventory. |
| `GET /goods-receipts/{id}` | `goods-receipts.view` | Retrieve a posted receipt. |
| `GET /goods-receipts/by-purchase-order/{purchaseOrderId}` | `goods-receipts.view` | List a PO's receipts. |

Example body for `POST /goods-receipts`:

```json
{
  "id": "41eb4c9f-554a-4345-9864-75198c7cc07d",
  "purchaseOrderId": "0396212c-605e-495e-8227-95cc15a03149",
  "lines": [
    { "purchaseOrderLineId": "b85564c7-ddc7-4f96-a468-115f4dc95e69", "quantityReceived": 70 }
  ]
}
```

Quantities must be positive; each PO line can appear once in a receipt; cumulative received quantities cannot exceed ordered quantities. A partial receipt changes the PO to `PartiallyReceived`; the last receipt changes it to `Received`. Draft, Cancelled, and already Received orders cannot receive more goods. Posted receipts and their lines cannot be edited or deleted through the Purchasing DbContext.

The API host composes the Purchasing and Inventory DbContexts on **one SQL Server connection and serializable transaction**. It saves the receipt and PO progress together with a `PurchaseReceipt` movement and `StockBalance` increase for every line, then commits once. A failure rolls back all changes. Each stock movement references the receipt ID and line number. The tenant must have Purchasing and Inventory enabled. The new Purchasing migration and permissions are applied by running `.\scripts\migrate-tenant.ps1 -TenantId dev` from `simplia-wear-runner` for a configured tenant.

`GoodsReceiptIntegrationTests` creates and removes an isolated SQL Server database when `SIMPLIAWEAR_INTEGRATION_CONNECTIONSTRING` is set. It checks a 70 + 30 partial receipt sequence, duplicate receipt rejection, and rollback after a forced inventory write failure.
