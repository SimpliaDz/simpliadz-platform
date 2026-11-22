# Functional backend tests

The functional suite checks business actions against isolated SQL Server test databases. It covers module services across Identity, Organization, Inventory, Purchasing, Sales, Accounting, Reporting, Audit, and CRM. The API test project also sends HTTP requests through the real endpoint pipeline for authentication, authorization, validation, brand changes, inventory configuration, printing, warnings, and audit history.

## Run the suite

From the repository root in PowerShell, with Docker Desktop and the .NET 10 SDK installed:

```powershell
./scripts/test-functional.ps1
```

The script starts the Compose SQL Server, waits for it to accept connections, and runs every test project in `src/SimpliaDz.sln` with `SIMPLIADZ_INTEGRATION_CONNECTIONSTRING` set. Each integration test uses a temporary database and deletes it afterward. The script does not delete the development tenant database. If the API is running and its build output is locked, build the changed test project separately and then use:

```powershell
./scripts/test-functional.ps1 -NoBuild
```

Do not use `dotnet test` without the integration connection string to verify the functional suite: database tests return early when that variable is absent. The script sets it from the local Docker Compose configuration. A passing run should show zero skipped and zero failed tests in every test project.

## What the tests prove

- Domain and service tests check state changes, invalid inputs, soft deactivation, stock ledger and balance consistency, repeated actions, transaction rollback, and selected permission checks.
- HTTP tests check representative route workflows and status codes, including unauthorized and forbidden requests, validation errors, immutable history, and single audit entries for login and stock adjustment.
- SQL Server is used for integration tests so EF Core mappings, migrations, constraints, and transactions are exercised with the production database engine.

The suite is a growing regression set. It does not assert every permutation of every endpoint. When adding a feature, add a test for its successful action, invalid input, authorization boundary, and any important retry or historical-data rule.
