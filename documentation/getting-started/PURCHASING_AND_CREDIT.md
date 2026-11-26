# Purchasing and customer credit

## Purchase orders and receiving

Save a draft with the supplier, receiving location, and notes. Add variants with
quantities and unit costs, then place the order to lock the lines. To use a new
variant, open the catalogue, create it, then refresh the variant selector in the
draft. Order totals are quantities multiplied by the recorded unit costs.
Separate tax and fee fields are outside the current purchase order model.

Receiving shows ordered, previously received, and remaining units. Each receipt
posts stock to the order's destination, can carry a delivery reference, and
links stock movements to the receipt. The order closes for receiving when all
units arrive.

## Supplier balances

Record a payment from a confirmed order. A 6,000 order with a 2,000 payment has
4,000 outstanding. Payment history is append-only. Overpayments and cancellation
of paid orders are blocked. The supplier account statement is available from
the supplier filter on Purchase orders. It shows total supplier debt and the
running balance before and after each payment. Use the statement filters to
narrow the displayed rows. Payment dates reflect the date entered by staff.
Supplier due dates are currently unavailable.

## Customer balances

POS checkout supports cash and card in one transaction. Select a customer to
complete a ticket with an unpaid balance. Inventory and the initial payments
post together. The receipt lists paid amounts by method and any remaining
balance.

Later payments apply to the sale selected by staff. From Sales, select the
customer and open an unpaid sale from the statement, then record a payment.
Entries are dated by the server and may include a reference. Multiple unpaid
sales remain separate. A sale with an identified customer and an outstanding
balance must be settled before a return is posted. The statement shows sales
and collected payments. It follows the existing return workflow, which tracks
the merchandise return separately from cash refunds.

## Barcode labels

Turn on **Show price on label** for a print job to include the retail price for
each selected variant. This works for one label or a batch. Prices come from
the first active Retail price list in the organization's currency, ordered by
name. A priced job stops if any selected variant lacks a retail price.

Apply the Purchasing and Sales migrations through the usual tenant migrator
before using the updated API.
