# PDF documents

SimpliaDz generates documents on the backend with [QuestPDF](https://www.questpdf.com/). PDFs are produced locally in the API process; customer or transaction data is not sent to an external PDF service.

## Licence configuration

The API configures `QuestPDF.Settings.License = LicenseType.Community` at startup. This is permitted only while the organisation that owns and benefits from SimpliaDz qualifies for the QuestPDF Community License (including businesses below USD 1M annual gross revenue). If that eligibility stops, change the configured licence type after obtaining the appropriate QuestPDF licence. See the [official licence guide](https://www.questpdf.com/license/guide.html).

## Current documents

| Document | Endpoint | Permission | Purpose |
| --- | --- | --- | --- |
| Pro-forma invoice | `GET /sales/{saleId}/documents/pro-forma.pdf` | `sales.view` | A4 quote/offer for a draft or recorded sale. It is explicitly marked as not being a fiscal invoice. |
| Sale receipt | `GET /sales/{saleId}/documents/receipt.pdf` | `sales.view` | Compact 80 mm-style receipt for a completed, partially returned, or returned sale. |

Both documents use the sale's stored line price, discount, and total snapshots. They do not use the current product price, so editing a product later cannot rewrite a historic sale document.

## Extension approach

New document types should add a document-specific data model and renderer behind the PDF service, then expose a permission-protected endpoint. Planned candidates are a fiscal invoice (after tax/legal numbering is designed), purchase order, goods-receipt note, inventory count sheet, and financial-report export.

Do not present a pro-forma as a fiscal invoice. Fiscal invoice numbering, tax registration fields, tax treatment, legal retention, and country-specific compliance must be agreed before that document type is introduced.
