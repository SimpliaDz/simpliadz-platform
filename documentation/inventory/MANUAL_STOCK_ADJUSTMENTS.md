# Manual Stock Adjustments

Authorized users can correct physical stock with `POST /stock/adjustments`. Use a signed `quantityDelta`: positive for an increase, negative for a decrease. A zero delta is rejected. The variant, location, and reason are required. Example:

```json
{
  "productVariantId": "00000000-0000-0000-0000-000000000001",
  "locationId": "00000000-0000-0000-0000-000000000002",
  "quantityDelta": -2,
  "reason": "Two damaged items"
}
```

The endpoint requires `stock.manage` and takes the actor from the authenticated user, never from request JSON. The variant and location must be active. A decrease that would make physical stock negative returns HTTP 409 and leaves the balance and ledger unchanged. Successful changes update `StockBalance` and append an `Adjustment` movement in the same transaction. The movement history can be read through `GET /stock/movements`.

For compatibility, the same endpoint still accepts the existing `quantityOnHand` form to set an absolute counted quantity. Supply exactly one of `quantityDelta` or `quantityOnHand`. Absolute recounts that do not change stock create no movement. Generic balance editing remains unavailable.
