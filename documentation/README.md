# SimpliaDz documentation

Start with [Run the backend locally](getting-started/LOCAL_START_GUIDE.md). The [MVP feature checklist](../features.md) describes what the backend currently supports and what still needs client confirmation. Each guide below covers one part of the system.

## Getting started

- [Run the backend locally](getting-started/LOCAL_START_GUIDE.md) — Docker, migrations, API, and login.
- [Local demo data](getting-started/DEMO_DATA.md) — repeatable sample users and business records for API testing.
- [Local tenancy](getting-started/LOCAL_TENANCY.md) — tenant configuration and API selection.

## Architecture

- [Technical guide](architecture/TECHNICAL_GUIDE.md) — solution structure and implementation patterns.
- [Module catalog](architecture/MODULE_CATALOG.md) — shared module registration.

## Identity and organization

- [Authentication](identity/AUTHENTICATION.md)
- [Organization profile](organization/ORGANIZATION_MODULE.md)
- [Worked hours](organization/WORKED_HOURS.md)

## Inventory

**Catalog:** [Products](inventory/PRODUCT_CATALOG.md), [Variants](inventory/PRODUCT_VARIANTS.md), [Attributes](inventory/PRODUCT_ATTRIBUTES.md), [Categories](inventory/PRODUCT_CATEGORIES.md), [Brands](inventory/BRANDS.md), and [Price lists](inventory/PRICE_LISTS.md).

**Stock:** [Inventory core](inventory/INVENTORY_CORE.md), [Locations](inventory/BUSINESS_LOCATIONS.md), [Movement ledger](inventory/STOCK_MOVEMENT_LEDGER.md), [Opening stock](inventory/OPENING_STOCK.md), [Manual adjustments](inventory/MANUAL_STOCK_ADJUSTMENTS.md), [Transfers](inventory/STOCK_TRANSFERS.md), and [Physical counts](inventory/PHYSICAL_INVENTORY_COUNTS.md).

**Configuration and printing:** [Low-stock thresholds](inventory/LOW_STOCK_THRESHOLDS.md), [Scheduled warnings](inventory/INVENTORY_WARNINGS.md), [Barcode labels](inventory/BARCODE_LABELS.md), and [Inventory documents](inventory/INVENTORY_DOCUMENTS.md).

## Purchasing

- [Suppliers](purchasing/SUPPLIERS.md)
- [Purchase orders](purchasing/PURCHASE_ORDERS.md)
- [Goods receipts](purchasing/GOODS_RECEIPTS.md)

## Sales and POS

- [Customers](sales/CUSTOMERS.md)
- [Sales](sales/SALES.md)
- [POS product lookup](sales/POS_PRODUCT_LOOKUP.md)
- [Discounts](sales/SALE_DISCOUNTS.md)
- [Payments](sales/SALE_PAYMENTS.md)
- [Returns](sales/SALE_RETURNS.md)
- [Cash registers](sales/CASH_REGISTERS.md)

## CRM

- [CRM and loyalty business MVP](crm/CRM_BUSINESS_MVP.md)
- [CRM technical guide](crm/CRM_TECHNICAL.md)

## Accounting, reporting, and audit

- Accounting: [Expense categories](accounting/EXPENSE_CATEGORIES.md) and [Expenses](accounting/EXPENSES.md).
- Reporting: [Dashboard summary](reporting/DASHBOARD_SUMMARY.md), [Sales summary](reporting/SALES_SUMMARY_REPORT.md), [Product performance](reporting/PRODUCT_PERFORMANCE_REPORT.md), [Inventory report](reporting/INVENTORY_REPORT.md), and [Low-stock report](reporting/LOW_STOCK_REPORT.md).
- Audit: [Business audit trail](audit/BUSINESS_AUDIT_TRAIL.md).

## Adding documentation

Put setup and operations guides in `getting-started`, cross-module design in `architecture`, and feature guides in the folder for the owning module. Add the new guide to this index and use relative Markdown links so navigation works in GitHub and local editors.
