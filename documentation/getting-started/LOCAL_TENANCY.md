# Local tenant configuration

For the full startup sequence, see [Start SimpliaDz locally](LOCAL_START_GUIDE.md).

SimpliaDz currently uses one database per tenant. During development, the shared
`src/tenants.json` file is the local tenant catalog used by both the API and the
database migrator. This avoids duplicating connection strings and module flags
between the two executable projects.

## Configuration shape

```json
{
  "Tenancy": {
    "ActiveTenantId": "dev",
    "Tenants": {
      "dev": {
        "Name": "Development Business",
        "ConnectionString": "...Database=SimpliaDz_Dev;...",
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

- `ActiveTenantId` selects the tenant served by the running API instance.
- `Tenants` may contain any number of local tenants.
- Each tenant owns its connection string and enabled-module flags.
- A module omitted from the map, or mapped to `false`, is disabled.

The project files link the same JSON file into both executable outputs. There is
only one checked-in source to update.

## Create and migrate a local tenant

1. Add a tenant entry to `src/tenants.json` with a unique database name.
2. Start SQL Server with `docker compose up -d`.
3. Run the migrator for that tenant:

   ```powershell
   dotnet run --project src\SimpliaDz.DatabaseMigrator -- --tenant demo
   ```

The migrator ignores `ActiveTenantId`; its explicit `--tenant` parameter decides
which descriptor, database, modules, migrations, and seeders it uses.

## Run the API for a tenant

By default, the API serves `Tenancy:ActiveTenantId` from `src/tenants.json`:

```powershell
dotnet run --project src\SimpliaDz.Api --launch-profile http
```

To test another tenant without editing the JSON file, override the active ID for
that process:

```powershell
$env:Tenancy__ActiveTenantId = "demo"
dotnet run --project src\SimpliaDz.Api --launch-profile http
Remove-Item Env:Tenancy__ActiveTenantId
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

`tenants.json` is a local-development mechanism. Do not commit production database
credentials. A deployed tenant API can receive the same keys from protected
environment configuration or a secret store. Later, `ITenantStore` can be backed
by a control-plane database without changing module DbContexts or business logic.
