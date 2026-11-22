# Start SimpliaDz locally

This guide starts the backend API against a local tenant on Windows PowerShell. Run the commands from the central development runner, `C:\.develop\.projects\SimpliaDz\simpliadz-runner`. You need the .NET 10 SDK and Docker Desktop running.

## 1. Check the tools and tenant

```powershell
cd 'C:\.develop\.projects\SimpliaDz\simpliadz-runner'
.\scripts\doctor.ps1
```

The runner's ignored `config/tenants.local.json` contains local tenants, SQL Server connection strings, and enabled modules. Its `config/local.json` selects which tenant the API serves. The migrator requires an explicit tenant ID. For first-time setup, copy the runner's `config/local.example.json` and `config/tenants.example.json`, then configure the private copies.

## 2. Start SQL Server

Start Docker Desktop first, then run:

```powershell
.\scripts\start-database.ps1
```

The `simpliadz-sql` container should show `Up` and expose port `1433`. Its data is kept in a Docker volume, so ordinary starts do not erase your tenant databases.

## 3. Apply migrations and seed data

```powershell
.\scripts\migrate-tenant.ps1 -TenantId dev
```

The migrator connects to the `dev` database, iterates over its enabled modules, calls each module's `MigrateAsync` and then `SeedAsync`, and finishes with `Migration completed.` EF Core applies only pending migrations; running this command again does not recreate tables or erase existing data. The latest MVP changes include Inventory migrations for barcode label templates, inventory document templates, and scheduled warnings, plus the Organization worked-hours migration. The seeders add the local admin user, permissions, and standard templates where applicable.

**Run the migrator before starting the API after pulling code with new migrations.** If it fails, resolve the reported SQL Server or migration error before testing the API. A successful `dotnet build` or `dotnet test` does not apply migrations to your local `dev` database.

To add sample users and connected business records for testing, include `--seed-demo`:

```powershell
.\scripts\migrate-tenant.ps1 -TenantId dev -SeedDemo
```

See [Local demo data](DEMO_DATA.md) for the records and sign-ins. This option is for local test tenants and is not part of the normal migration command.

## 4. Start the API

Open a **second PowerShell terminal** in the runner folder and leave it running:

```powershell
cd 'C:\.develop\.projects\SimpliaDz\simpliadz-runner'
.\scripts\start-api.ps1 -TenantId dev
```

Wait for `Now listening on: http://0.0.0.0:5278`. The runner sets `ASPNETCORE_ENVIRONMENT=Development`; Swagger is available at <http://localhost:5278/swagger>. Stop the API with `Ctrl+C` when finished.

## 5. Check the API and log in

In another PowerShell terminal:

```powershell
$baseUrl = 'http://localhost:5278'
Invoke-RestMethod "$baseUrl/health/live"

$loginBody = @{ email = 'admin@dev.local'; password = 'Admin123!' } | ConvertTo-Json
$login = Invoke-RestMethod "$baseUrl/auth/login" -Method Post -ContentType 'application/json' -Body $loginBody
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
Invoke-RestMethod "$baseUrl/auth/me" -Headers $headers
```

The health endpoint is system-level and needs no tenant header or token. Login and `/auth/me` use the API instance's active tenant. The seeded credentials are for local development; if this database already existed and its admin password was changed, the seeder does not reset that password. Keep access and refresh tokens out of shared logs.

You can also use [`src/SimpliaDz.Api/SimpliaDz.Api.http`](../../src/SimpliaDz.Api/SimpliaDz.Api.http) or Swagger for manual requests.

## Test another local tenant

Migrate the other tenant's database first:

```powershell
.\scripts\migrate-tenant.ps1 -TenantId demo
```

Then start a **separate API process** for that tenant (stop the `dev` API first if it is using port `5278`):

```powershell
.\scripts\start-api.ps1 -TenantId demo
```

The API does not accept an `X-Tenant-Id` header. Its active tenant controls the database, and a token issued for one tenant cannot be used on an API instance assigned to another tenant. See [local tenancy](LOCAL_TENANCY.md) for the configuration model.
