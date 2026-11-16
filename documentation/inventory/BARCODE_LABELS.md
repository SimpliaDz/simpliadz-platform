# Barcode label templates and print documents

Barcode label templates belong to the current tenant's Inventory database. The migration seeds a standard A4 page with 90 × 40 mm labels. Admins can create other layouts using page width and height, label width and height, page margins, and spacing, all in millimeters. A template can show or hide the product name and SKU. Deactivation keeps the saved template record but prevents further printing with it.

| Endpoint | Permission | Purpose |
| --- | --- | --- |
| `GET /barcode-label-templates` | `barcode-labels.view` | List templates, including inactive ones. |
| `POST /barcode-label-templates` | `barcode-labels.manage` | Create a template. |
| `PUT /barcode-label-templates/{id}` | `barcode-labels.manage` | Update dimensions and display fields. |
| `PUT /barcode-label-templates/{id}/deactivate` | `barcode-labels.manage` | Deactivate a template. |
| `POST /barcode-labels/print-document` | `barcode-labels.print` | Generate printable labels. |

Example template body:

```json
{
  "name": "Shop A4 90 x 40",
  "pageWidthMm": 210,
  "pageHeightMm": 297,
  "labelWidthMm": 90,
  "labelHeightMm": 40,
  "marginMm": 5,
  "gapMm": 3,
  "showName": true,
  "showSku": true
}
```

Example print body:

```json
{
  "templateId": "5369ed28-f57a-49be-a587-13f4d0d091f1",
  "lines": [
    { "variantId": "11111111-1111-1111-1111-111111111111", "quantity": 3 }
  ]
}
```

The print endpoint returns a standalone HTML document containing vector SVG pages. It renders the barcode using Code 128 and supports up to 500 labels per request. The selected variants and their products must be active and have a barcode. If a barcode is too dense for the selected label width, the API rejects the request so the admin can select or configure a wider template. The endpoint does not contact a printer; the consuming app can display or print the returned document.
