# Run the complete API collection in Postman

The importable collection exercises every mapped API endpoint method. It creates its own records, saves returned IDs, and runs happy-path requests followed by authentication, permission, validation, pagination, or missing-record checks where applicable. It is intended for a **local test tenant** because it creates sales, stock movements, purchase receipts, audit records, and soft deactivations.

## 1. Start the dedicated test API

From the repository root, with Docker Desktop and the .NET 10 SDK available:

```powershell
./scripts/start-postman-api.ps1
```

The script starts SQL Server, migrates and seeds the `postman` tenant in its own `SimpliaWear_Postman_postman` database, enables all modules through process environment variables, and starts the API at `http://localhost:5288`. It does not edit the runner's private catalog or switch your normal API's active tenant. Leave this terminal open while Postman runs. Stop it with Ctrl+C afterward.

On a fresh database, Identity seeds `admin@dev.local` with password `Admin123!`. If the password was changed previously, use its current value. The migrator does not reset an existing password. The script does not erase the test database between runs; unique names let the collection run again.

If another API process or Visual Studio holds the normal Debug build output open, use a current Release build and select it in the script. `-NoBuild` is available only after the projects have already been built with the current source:

```powershell
dotnet build src/SimpliaWear.sln -c Release
./scripts/start-postman-api.ps1 -Configuration Release -NoBuild
```

## 2. Import and run

1. Import [the collection](../../postman/SimpliaWear.postman_collection.json) and [the local environment](../../postman/SimpliaWear.local.postman_environment.json) into Postman.
2. Select **SimpliaWear Local API** as the active environment. Its `baseUrl` is `http://localhost:5288`.
3. Set the environment's `adminPassword` current value to the seeded or current admin password. The checked-in environment leaves this secret blank.
4. Run the **entire collection** once in Collection Runner, in its saved order, with one iteration. The first requests check health, log in, and create a restricted user. Later requests use saved IDs and tokens. Running a single folder skips its prerequisites.
5. Inspect failed assertions in the Runner. The test database retains the records so you can inspect them through the API or SQL Server.

The API selects the tenant from its startup configuration. Do not add an `X-Tenant-Id` header. The collection's access and refresh tokens live in Postman collection variables during the run. Avoid exporting a populated environment or collection with real credentials or tokens.

## Coverage and regeneration

The collection is generated from the 187 endpoint mappings under `src/SimpliaWear.Api/Endpoints`, plus the root and two health checks. It currently contains 682 requests: authorized workflows, unauthenticated checks, restricted-user permission checks, targeted invalid-input, pagination, and missing-record cases, and read-back checks for soft deactivation, stock, audit, and the saved user language. Some system and public authentication endpoints have their own negative cases rather than permission checks.

When endpoint mappings change, update the scenarios in `scripts/generate-postman.mjs`, then regenerate both import files:

```powershell
node scripts/generate-postman.mjs
```

The generator fails when the route count changes, prompting a review of new actions and required request bodies. The local verifier used during development can rerun the collection's HTTP sequence against the dedicated API:

```powershell
node scripts/verify-postman.mjs
```

The verifier checks response statuses, ID chaining, and explicit state assertions. Postman's Runner evaluates the collection's full assertions and is the final import/run check.

The regenerated 682-request collection passed the local HTTP verifier against the dedicated test tenant, including the language workflow. The verifier checks expected statuses and executes collection scripts; run it in Postman's Collection Runner to evaluate Postman's full assertions.
