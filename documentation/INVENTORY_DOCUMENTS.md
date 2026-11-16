# Inventory document templates

Inventory document templates live in each tenant's Inventory database. The migration seeds three templates: stock list, physical count sheet, and count differences. A manager can create and edit templates with a name, title, selected columns, A4 or A3 paper, portrait or landscape orientation, and optional footer. Deactivation is the soft-delete operation for templates.

| Endpoint | Permission | Purpose |
| --- | --- | --- |
| `GET /inventory-document-templates` | `inventory-documents.view` | List templates. |
| `POST /inventory-document-templates` | `inventory-documents.manage` | Create a template. |
| `PUT /inventory-document-templates/{id}` | `inventory-documents.manage` | Update a template. |
| `PUT /inventory-document-templates/{id}/deactivate` | `inventory-documents.manage` | Deactivate a template. |
| `GET /reports/inventory/document?templateId=...&locationId=...&includeZeroStock=true` | `inventory-documents.print` | Print current stock, optionally including zero-stock variants. |
| `GET /inventory-counts/{id}/count-sheet?templateId=...` | `inventory-documents.print` | Print a blank count sheet. |
| `GET /inventory-counts/{id}/difference-document?templateId=...` | `inventory-documents.print` | Print nonzero differences from a completed count. |

Create body example:

```json
{
  "name": "Monthly stock list",
  "type": "StockList",
  "title": "Monthly inventory",
  "columns": ["Product", "Sku", "Barcode", "Quantity"],
  "paperSize": "A4",
  "landscape": false,
  "footerText": "Checked by: __________"
}
```

Supported types and columns:

- `StockList`: `Product`, `Sku`, `Barcode`, `Quantity`.
- `CountSheet`: `Product`, `Sku`, `Barcode`, `Expected`, `Counted`. The printed `Counted` column is blank.
- `CountDifferences`: `Product`, `Sku`, `Barcode`, `Expected`, `Counted`, `Difference`.

The print endpoints return standalone HTML with print page settings and repeated table headers. Product and template text is encoded before rendering. The stock list limits each document to 5,000 variants; use a location filter or split larger catalogs. The backend does not send the document directly to a printer.
