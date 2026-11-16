# Sale Discounts (SMP-41)

V1 sale discounts support `FixedAmount` and `Percentage` on a draft sale or one of its lines. A line percentage is calculated from `Quantity × UnitPrice`. A sale percentage is calculated from the sum of line totals after line discounts. Percentage values must be between 0 and 100; fixed discounts cannot exceed the amount they reduce. Values and calculated amounts use four decimal places, with percentage results rounded away from zero at the fourth decimal place.

| Route | Body | Required permissions |
|---|---|---|
| `PUT /sales/{id}/lines/{lineId}/discount` | `{ "type": "Percentage", "value": 10 }` | `sales.manage` and `sales.discounts.apply` |
| `PUT /sales/{id}/discount` | `{ "type": "FixedAmount", "value": 500 }` | `sales.manage` and `sales.discounts.apply` |

`POST /sales/{id}/lines` still accepts the SMP-39 `discountAmount` field. A positive value requires `sales.discounts.apply` in addition to `sales.manage`; zero is allowed for ordinary cashiers. The API returns 403 for a user who can manage sales but cannot apply discounts. The dedicated routes allow replacing a discount while the sale is a draft. Completed or cancelled sales cannot be discounted.

The Sales migration stores discount type, requested value, and calculated amount on both the sale and sale lines. The completed sale therefore retains the actual discount amount and the basis used to calculate it. Totals are recalculated on the server and cannot become negative. If a line discount changes after a sale percentage is set, the sale percentage amount is recalculated; a fixed sale discount that would exceed the new line total is rejected. Run the tenant migrator to apply `Sales_Discounts` and seed the new permission into the Admin role.
