# Simplia Module Catalog

## Purpose

`SimpliaDz.ModuleCatalog` is a **composition project**, not a business module. It owns the authoritative list of modules installed in this deployment. Both executable hosts use this list:

```text
SimpliaDz.ModuleCatalog
          │
          ├── SimpliaDz.Api             registers enabled modules at startup
          └── SimpliaDz.DatabaseMigrator migrates and seeds enabled modules
```

Keeping the list here prevents the API and database migrator from silently maintaining different installer lists.

## What it contains

The project has one public class, `ModuleCatalog`:

```csharp
public static IReadOnlyList<IModuleInstaller> GetModules() =>
[
    new IdentityModuleInstaller(),
    new InventoryModuleInstaller(),
    // remaining installed modules
];
```

Each item is an `IModuleInstaller` implemented by a module's Infrastructure project. The catalog does not contain domain entities, endpoints, use cases, migrations, seed data, tenant rules, or business logic.

## Why this is a separate project

`SimpliaDz.BuildingBlocks` defines `IModuleInstaller` and is referenced by every module Infrastructure project. The catalog must reference the concrete Infrastructure installers, so it cannot live in BuildingBlocks without creating a circular dependency:

```text
BuildingBlocks  ←  Identity.Infrastructure
       ▲                    │
       └────────────────────┘  circular dependency (invalid)
```

It also should not live in `SimpliaDz.Api` or `SimpliaDz.DatabaseMigrator`: those are independent hosts, and neither should reference the other. The catalog is their small, neutral shared composition point.

## Installed versus enabled modules

The catalog answers **what is installed in the codebase**. Tenant configuration answers **what is enabled for a host and tenant**.

```text
catalog contains all 10 installers
             │
             ▼
host reads Tenancy:Tenants:<tenant>:Modules
             │
             ▼
only enabled installers are registered, migrated, and seeded
```

The API registers the installed modules once at startup and serves the tenant selected by `Tenancy:ActiveTenantId`. The migrator independently selects a tenant with `--tenant`. The runner passes the same private catalog path to both processes through `SIMPLIADZ_TENANTS_FILE`; each activates or rejects modules using the selected tenant's flags.

## Adding a module

When a new module is created:

1. Implement `IModuleInstaller` in the module's Infrastructure project.
2. Add the Infrastructure project reference to `SimpliaDz.ModuleCatalog.csproj`.
3. Add the installer to `ModuleCatalog.GetModules()`.
4. Add an explicit flag for the module under every relevant tenant in the runner's private `config/tenants.local.json`.
5. Add the module's UseCases assembly to the API Mediator/FluentValidation scan and map its endpoints when it has HTTP behavior.
6. Update the catalog test's expected module list.

An installed scaffold module can be enabled and have its infrastructure registered, but it does not become a usable API feature until it has handlers and endpoint mappings.

## Test coverage

`tests/SimpliaDz.ModuleCatalog.Tests` asserts that the catalog returns the expected ten modules exactly once. Run it with:

```powershell
dotnet test tests\SimpliaDz.ModuleCatalog.Tests\SimpliaDz.ModuleCatalog.Tests.csproj
```
