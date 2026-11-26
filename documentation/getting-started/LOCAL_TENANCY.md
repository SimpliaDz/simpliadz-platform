# Local tenant configuration

For the full startup sequence, see [Start SimpliaWear locally](LOCAL_START_GUIDE.md).

SimpliaWear uses one database per tenant. During local development, the runner's
ignored `config/tenants.local.json` is the single catalog used by both the API and
the database migrator. The runner passes its absolute path through
`SIMPLIAWEAR_TENANTS_FILE`; a deployed process may supply tenant configuration
through environment variables or another protected provider.

## Configuration shape

```json
{
  "Tenancy": {
    "Tenants": {
      "dev": {
        "Name": "Development Business",
        "ConnectionString": "...Database=SimpliaWear_Dev;...",
        "Modules": {
          "Identity": true,
          "Inventory": true,
          "Organization": true,
          "Purchasing": true
        }
      }
    }
  }
}
```

- The runner's `config/local.json` selects the active API tenant with `api.activeTenantId`.
- `Tenants` may contain any number of local tenants.
- Each tenant owns its connection string and enabled-module flags.
- A module omitted from the map, or mapped to `false`, is disabled.

The catalog is read from its external path at startup. It is not copied into
either executable output or committed with database credentials.

## Create and migrate a local tenant

1. Add a tenant entry to the runner's `config/tenants.local.json` with a unique database name.
2. Start SQL Server with `./scripts/start-database.ps1` from the runner.
3. Run the migrator for that tenant:

   ```powershell
   ./scripts/migrate-tenant.ps1 -TenantId demo
   ```

The migrator ignores the runner's active API tenant; its explicit `--tenant` parameter decides
which descriptor, database, modules, migrations, and seeders it uses.

## Run the API for a tenant

By default, the runner starts the API for `api.activeTenantId` from `config/local.json`:

```powershell
./scripts/start-api.ps1
```

To test another tenant without editing the JSON file, pass an override to the runner:

```powershell
./scripts/start-api.ps1 -TenantId demo
```

Clients do not send an `X-Tenant-Id` header. The API instance, not the caller,
chooses the database. Login tokens contain the selected tenant ID and are rejected
if used by an API instance assigned to a different tenant.

## Tenant-scoped and system-scoped endpoints

Tenant endpoint groups carry `TenantRequiredMetadata`. The tenant middleware then:

1. resolves the configured active tenant;
2. checks whether the endpoint's module is enabled;
3. provides the tenant connection string to module `DbContext` instances.

System endpoints do not carry that metadata and do not resolve a tenant:

- `GET /`
- `GET /health/live`
- `GET /health/ready`
- Swagger in Development

When invitation endpoints are implemented, the invitation link should route the
user to the correct tenant-specific API or host. The invitation recipient should
never need to know or manually enter a tenant header.

## Production direction

`SIMPLIAWEAR_TENANTS_FILE` is optional for processes configured entirely through
environment variables. The private catalog is for local development. A deployed
tenant API should receive the same keys from protected configuration or a secret
store. Later, `ITenantStore` can be backed by a control-plane database without
changing module DbContexts or business logic.
