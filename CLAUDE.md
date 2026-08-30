# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with this repository.

---

## Commands

**Start infrastructure (SQL Server via Docker):**
```bash
docker compose up -d
```

**Build the solution:**
```bash
dotnet build src/SimpliaDz.sln
```

**Run the API** (http://localhost:5278):
```bash
cd src/SimpliaDz.Api && dotnet run
```

**Run migrations + seed data for a tenant:**
```bash
cd src/SimpliaDz.DatabaseMigrator && dotnet run --tenant dev
```

**Add an EF Core migration:**
```bash
# Requires dotnet-ef tool: dotnet tool restore
dotnet ef migrations add <MigrationName> \
  --project src/Modules/<Module>/SimpliaDz.<Module>.Infrastructure \
  --startup-project src/SimpliaDz.Api
```

**Tests:** None yet — `tests/` directory is empty.

---

## Stack & Key Libraries

| Concern | Library | Version |
|---|---|---|
| Framework | ASP.NET Core (Minimal APIs) | .NET 10 |
| Mediator / CQRS | MediatR | 14.0.0 |
| Validation | FluentValidation | 12.1.1 |
| ORM | EF Core (SQL Server) | 10.0.2 |
| Identity | ASP.NET Core Identity (EF stores) | 10.0.2 |
| Authentication | Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.2 |
| API Docs | Swashbuckle.AspNetCore | 10.1.2 |
| Database | SQL Server 2022 (Docker) | — |

All module projects follow the same dependency rules: `Core` has no NuGet packages (framework references allowed); `UseCases` adds MediatR + FluentValidation; `Infrastructure` adds EF Core + SQL Server + AspNetCore.App. Domain entities always live in `Core`, never in `Infrastructure`.

---

## Project Layout

```
src/
├── SimpliaDz.Api/                    # ASP.NET Core host — Minimal API endpoints, DI wiring
│   ├── Configuration/JwtOptions.cs
│   ├── Endpoints/
│   │   ├── Identity/                 # AuthEndpoints, UserEndpoints, RoleEndpoints, PermissionEndpoints
│   │   └── Inventory/                # ProductEndpoints
│   ├── Extensions/
│   │   ├── AuthenticationExtensions.cs
│   │   ├── DependencyInjectionExtensions.cs
│   │   ├── EndpointExtensions.cs
│   │   └── MiddlewareExtensions.cs
│   ├── Modules/ModuleRegistry.cs
│   └── Program.cs
│
├── SimpliaDz.BuildingBlocks/         # Shared cross-cutting infrastructure
│   ├── Exceptions/NotFoundException.cs
│   ├── Mediation/ValidationBehavior.cs
│   ├── Modules/Abstractions/IModuleInstaller.cs
│   └── Tenancy/                      # ITenantContext, ITenantResolver, ITenantStore, TenancyOptions
│
├── SimpliaDz.SharedKernel/           # Reserved for shared domain primitives (currently empty)
├── SimpliaDz.DatabaseMigrator/       # CLI tool — runs EF migrations + seeders per tenant
│
└── Modules/
    ├── Identity/                     # ACTIVE — auth, users, roles, permissions
    ├── Inventory/                    # ACTIVE — products, variants, brands, categories, stock
    ├── Audit/                        # stub
    └── Sales|Purchasing|Accounting|Notifications|Reporting  # stubs
```

---

## Architecture

### Modular Monolith

Each module is fully isolated: it has its own `DbContext`, its own DB schema, its own DI registrations, and its own migrations. No module accesses another module's `DbContext` or tables directly.

Modules are discovered via `ModuleRegistry.GetModules()` which returns a list of `IModuleInstaller` implementations. Both `SimpliaDz.Api` and `SimpliaDz.DatabaseMigrator` consume this registry.

Currently active modules in `ModuleRegistry`: **Identity**, **Inventory**.

### Module Layers (every module)

```
<Module>.Core            — Domain entities, value objects, domain exceptions.
                           No NuGet package dependencies.
                           Framework references (Microsoft.AspNetCore.App) are allowed when the
                           domain model requires framework types (e.g. Identity module entities
                           extend IdentityUser<Guid> / IdentityRole<Guid>).

<Module>.UseCases        — MediatR commands/queries/handlers, FluentValidation validators,
                           repository/service interfaces (abstractions only, no EF).
                           Depends on: MediatR, FluentValidation, BuildingBlocks, <Module>.Core.

<Module>.Infrastructure  — EF Core DbContext, entity type configurations (IEntityTypeConfiguration),
                           migrations, repository/service implementations, seeders, IModuleInstaller.
                           Depends on: EF Core + SqlServer, Microsoft.AspNetCore.App, <Module>.Core,
                           <Module>.UseCases.
                           Does NOT define domain entities — those live in Core.

<Module>.Contracts       — Public request/response DTOs shared with the API layer.
                           No dependencies. Consumed by API endpoints.
```

### CQRS with MediatR (Vertical Slicing)

Each use case lives in its own folder under `UseCases/<Domain>/<Commands|Queries>/<UseCaseName>/`:
- `<Name>Command.cs` or `<Name>Query.cs` — the MediatR `IRequest<T>` record
- `<Name>Handler.cs` — the `IRequestHandler<TRequest, TResponse>` implementation
- `<Name>Validator.cs` — optional FluentValidation `AbstractValidator<TRequest>`

The handler receives injected service/repository interfaces (never EF directly in UseCases). The Infrastructure layer implements those interfaces.

### MediatR Pipeline

`ValidationBehavior<TRequest, TResponse>` runs before every handler. It collects all registered `IValidator<TRequest>` implementations, runs them in parallel, and throws `FluentValidation.ValidationException` if any fail. This is registered as an open-generic pipeline behavior.

### Global Exception Handling

`MiddlewareExtensions.UseAppMiddleware()` registers a central `UseExceptionHandler` that maps:

| Exception type | HTTP status |
|---|---|
| `ValidationException` | 400 — body includes `{ Errors: [{ PropertyName, ErrorMessage }] }` |
| `InvalidOperationException` | 400 — body includes message |
| `NotFoundException` | 404 |
| `UnauthorizedAccessException` | 401 |
| anything else | 500 |

### Minimal API Endpoints

Endpoints use explicit binding attributes on every parameter (required by .NET 10):
- `[FromBody]` on DTO parameters
- `[FromServices]` on injected services (`ISender`, etc.)
- `[FromQuery]` on query string parameters
- Route values (`Guid id`) need no attribute
- `CancellationToken` and `ClaimsPrincipal` are auto-bound — no attribute needed

`using Microsoft.AspNetCore.Mvc;` is required for the binding attributes.

### Multi-Tenancy

- Tenants configured in `appsettings.json` under `Tenancy.Tenants`
- Each tenant has: `Name`, `ConnectionString`, `Modules` (feature flags per module)
- `IModuleInstaller.Register()` receives the tenant-specific connection string and wires up `DbContext` for that tenant
- `ITenantContext` exposes the current tenant's info (ID, connection string, module flags)
- `ITenantResolver` resolves the active tenant — `CliTenantResolver` uses `--tenant <id>` from CLI args; HTTP variant resolves from the request
- `ConfigurationTenantStore` reads tenant definitions from `IConfiguration`
- `DatabaseMigrator` runs with `--tenant <name>` to target the right DB

Dev tenant: `dev` → `SimpliaDz_Dev` database on `localhost,1433`.

### Authentication & Authorization

- JWT Bearer tokens issued at `POST /auth/login`
- `JwtOptions` (section `Jwt`): `Issuer`, `Audience`, `SigningKey` (min 32 chars), `ExpiresMinutes` (default 60)
- JWT claims include: `NameIdentifier` (user ID), `Email`, `Role` (ASP.NET Identity roles), `permission` (custom claims, one per permission)
- Authorization policies come from the centralized `SharedKernel/Authorization/PermissionNames.cs` catalog.
- Endpoints call `.RequireAuthorization(PermissionNames.<Area>.<Action>)` — no `[Authorize]` attributes and no repeated permission literals.

Dev credentials (seeded): `admin@dev.local` / `Admin123!`

---

## Identity Module Detail

**DB Schema:** `identity`

**Entities** (`Core/Entities/`):
- `AppUser : IdentityUser<Guid>` — adds `FirstName`, `LastName`, `IsActive`
- `AppRole : IdentityRole<Guid>` — adds `RolePermissions` nav property
- `Permission` — `Id` (int), `Name`, `DisplayName`, `Category`
- `RolePermission` — join between `AppRole` and `Permission`

**Services (Infrastructure → UseCases interface):**
- `IIdentityAuthService` / `IdentityAuthService` — login, forgot/reset password, change password
- `IJwtTokenGenerator` / `JwtTokenGenerator` — builds JWT with role + permission claims
- `IIdentityUserService` / `IdentityUserService` — create, activate/deactivate, assign/remove roles
- `IIdentityRoleService` / `IdentityRoleService` — create role, assign/remove permissions
- `IPermissionService` / `PermissionService` — list permissions

**Seeded permissions** (12 total, idempotent):
`users.view`, `users.create`, `users.edit`, `users.delete`,
`roles.view`, `roles.manage`,
`permissions.view`,
`products.view`, `products.create`, `products.edit`, `products.delete`, `products.manage`

**Migrations:**
- `Identity_Initial` — base Identity tables
- `Identity_PermissionsAndProfile` — `Permission`, `RolePermission`, user profile fields

**Endpoints:**
```
POST /auth/login
GET  /auth/me                    [RequireAuthorization]
POST /auth/change-password       [RequireAuthorization]
POST /auth/forgot-password
POST /auth/reset-password

GET  /users                      [users.view]
GET  /users/{id}                 [users.view]
POST /users                      [users.create]
POST /users/{id}/roles           [users.edit]
DEL  /users/{id}/roles           [users.edit]
PUT  /users/{id}/activate        [users.edit]
PUT  /users/{id}/deactivate      [users.edit]
GET  /users/{id}/permissions     [users.view]

GET  /roles                      [roles.view]
GET  /roles/{id}                 [roles.view]
POST /roles                      [roles.manage]
POST /roles/{id}/permissions     [roles.manage]
DEL  /roles/{id}/permissions     [roles.manage]

GET  /permissions                [permissions.view]
```

---

## Inventory Module Detail

**DB Schema:** `inventory`

**Core entities:**
- `Product` (aggregate root) — `Id`, `Name` (value object), `Code`, `Description`, `BrandId`, `ProductCategoryId`, `IsActive`, `CreatedAt`, `UpdatedAt`; has `Variants`
- `ProductVariant` — `Id`, `ProductId`, `Sku` (value object), `RetailPrice`, `WholesalePrice`, `Barcode`, `Color`, `Size`
- `Brand` — `Id`, `Name`
- `ProductCategory` — `Id`, `Name`
- `InventoryLocation` — `Id`, `Name`, `Code`, `Type` (`Store`/`Warehouse`), `Address`, `Phone`, `IsActive`, timestamps
- `StockItem` — tracks `QuantityOnHand` per variant per location; methods: `ApplyDelta`, `SetQuantity`
- `StockMovement` — ledger entries (Receipt, Issue, Adjustment) with reference info

**Value objects:**
- `ProductName` — non-empty, max 200 chars
- `Sku` — format-validated SKU string

**Domain exceptions:** `DomainException`, `InsufficientStockException`, `InvalidProductNameException`, `InvalidSkuException`

**Repository pattern:** `IProductRepository`/`ILocationRepository` (UseCases) → EF implementations (Infrastructure)

**Migrations:**
- `Inventory_Foundation_Initial`
- `Inventory_BusinessLocations`

**Seeded data:** 2 locations, 4 brands, 4 categories, 4 products with variants and initial stock

**Endpoints:**
```
POST /products                   [products.create]
GET  /locations                  [locations.view]
GET  /locations/{id}             [locations.view]
POST /locations                  [locations.create]
PUT  /locations/{id}             [locations.edit]
PUT  /locations/{id}/deactivate  [locations.edit]
```

---

## Adding a New Module

1. Create four projects following existing naming: `SimpliaDz.<Module>.Core`, `.UseCases`, `.Infrastructure`, `.Contracts`
2. In `Infrastructure`, implement `IModuleInstaller` — set `ModuleName`, implement `Register()`, `MigrateAsync()`, `SeedAsync()`
3. Add the installer to `ModuleRegistry.GetModules()` in `SimpliaDz.Api/Modules/ModuleRegistry.cs`
4. Register the same installer in `SimpliaDz.DatabaseMigrator/Program.cs`
5. Enable the module for relevant tenants in `appsettings.json` under `Tenancy.Tenants.<tenant>.Modules`
6. Add MediatR assembly scanning for the new `UseCases` assembly in `DependencyInjectionExtensions.cs`
7. Add endpoint mapping in `EndpointExtensions.cs`

### IModuleInstaller contract

```csharp
public interface IModuleInstaller
{
    string ModuleName { get; }
    void Register(IServiceCollection services, string connectionString);
    Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
    Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
```

---

## Configuration Reference

**`appsettings.json` sections:**

```json
{
  "Jwt": {
    "Issuer": "SimpliaDz",
    "Audience": "SimpliaDz",
    "SigningKey": "<min 32 chars>",
    "ExpiresMinutes": 60
  },
  "Tenancy": {
    "Tenants": {
      "dev": {
        "Name": "Development",
        "ConnectionString": "Server=localhost,1433;Database=SimpliaDz_Dev;...",
        "Modules": {
          "Identity": true,
          "Inventory": true,
          "Sales": false
        }
      }
    }
  }
}
```

**Docker `.env`:** `SA_PASSWORD`, `MSSQL_PORT`, `MSSQL_DATA_VOLUME`
