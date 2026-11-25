# Organization Module

## Purpose

The Organization module stores the business profile for the current tenant. It is a business module with the standard `Core`, `UseCases`, `Infrastructure`, and `Contracts` projects. The profile can later be consumed by POS, receipts, reports, and configuration features without putting organization data into Identity or another unrelated module.

## Tenant model

Each tenant uses its own configured database connection. The Organization module stores one profile row in that tenant database, using the fixed profile ID `00000000-0000-0000-0000-000000000001`. The same ID may exist in every tenant database without sharing data.

The API instance selects its tenant from the runner's private catalog through `SIMPLIADZ_TENANTS_FILE`; callers do not provide a tenant header. The runner's `api.activeTenantId` selects the local API tenant.

The module must also be enabled under `Tenancy:Tenants:<tenant>:Modules:Organization`.

## Data model

The `Organization` entity stores:

- `Id`
- `Name`
- `LegalName`
- `Phone`
- `Email`
- `Address`
- `CurrencyCode`
- `DefaultLanguage`
- `TimeZoneId`
- `IsActive`
- `CreatedAt`
- `UpdatedAt`

It is mapped to `organization.Organizations`. Currency codes are stored as three uppercase ASCII letters. When the first configuration request omits a currency, the entity uses `DZD`. The time zone defaults to `Africa/Algiers` and is used by worked-hours reports.

## API

### Retrieve the profile

```http
GET /organization
Authorization: Bearer <token>
```

Required permission: `organization.view`.

The endpoint returns `200 OK` with the profile, or `404 Not Found` when the tenant has not configured a profile yet.

### Configure or update the profile

```http
PUT /organization
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Simplia Store",
  "legalName": "Simplia SARL",
  "phone": "+213555000000",
  "email": "contact@example.dz",
  "address": "Algiers, Algeria",
  "currencyCode": "DZD",
  "defaultLanguage": "fr",
  "isActive": true
}
```

Required permission: `organization.manage`.

`PUT` is an upsert: the first valid request creates the tenant profile and subsequent requests update it. It returns `200 OK` with the saved profile.

## Validation and business rules

- `Name` is required and limited to 200 characters.
- Optional fields have database-aligned maximum lengths.
- A supplied email must be valid.
- A supplied currency must contain exactly three letters and is normalized to uppercase.
- An omitted or blank currency defaults to `DZD`.
- Validation failures use the API's standard `400 ValidationError` response.
- Disabled-module requests return `404 Not Found`.
- JWT tenant binding prevents a token issued for one tenant from being used for another tenant.

## Permissions and migration

The Identity seeder adds `organization.view` and `organization.manage`; the development Admin role receives all seeded permissions. Run the tenant migrator after deployment so it creates the `organization` schema and updates Identity permissions:

```powershell
.\scripts\migrate-tenant.ps1 -TenantId dev
```

## Tests

`tests/SimpliaDz.Organization.Tests` covers the DZD default, currency normalization, invalid name/currency validation, and create-then-update behavior.
