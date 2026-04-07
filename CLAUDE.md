# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

**Start infrastructure:**
```bash
docker compose up -d
```

**Build:**
```bash
dotnet build src/SimpliaDz.sln
```

**Run API** (http://localhost:5278):
```bash
cd src/SimpliaDz.Api && dotnet run
```

**Run migrations & seed data for a tenant:**
```bash
cd src/SimpliaDz.DatabaseMigrator && dotnet run --tenant dev
```

**Add an EF Core migration** (requires `dotnet ef` tool, installed via `dotnet tool restore`):
```bash
dotnet ef migrations add <MigrationName> --project src/Modules/<Module>/<Module>.Infrastructure --startup-project src/SimpliaDz.Api
```

**Tests:** No tests exist yet (`tests/` directory is empty).

## Architecture

This is a **modular monolith** with multi-tenancy and DDD patterns.

### Project Layout

```
src/
├── SimpliaDz.Api                 # ASP.NET Core entry point (Minimal APIs, DI wiring)
├── SimpliaDz.DatabaseMigrator    # CLI tool — runs EF migrations + seeders per tenant
├── SimpliaDz.BuildingBlocks      # Shared infrastructure: MediatR pipeline, module/tenancy abstractions
├── SimpliaDz.SharedKernel        # Cross-cutting domain types (placeholder)
└── Modules/
    ├── Inventory/                # Product catalog, stock (ACTIVE)
    ├── Identity/                 # Users, roles, JWT auth (ACTIVE)
    ├── Audit/                    # Change tracking (stub)
    └── Sales|Purchasing|Accounting|Notifications|Reporting  # Stubs
```

### Module Structure

Every module follows four layers:
- **Core** — Domain entities, value objects, domain exceptions (no external dependencies)
- **UseCases** — MediatR commands/queries, handlers, FluentValidation validators
- **Infrastructure** — EF Core `DbContext`, migrations, repository implementations, `IModuleInstaller`
- **Contracts** — Public DTOs/interfaces (currently placeholder files)

Each module registers itself via `IModuleInstaller`. The API's `Program.cs` and the `DatabaseMigrator` discover and invoke these installers.

### Request Flow

1. Minimal API endpoint receives HTTP request
2. MediatR dispatches to the appropriate command/query handler
3. `ValidationBehavior` (MediatR pipeline) runs FluentValidation before the handler; throws `ValidationException` on failure
4. Global `ExceptionHandler` middleware converts `ValidationException` → HTTP 400 with field-level error details

### Multi-Tenancy

- Tenants are configured in `appsettings.json` under `Tenancy.Tenants`
- Each tenant has its own connection string and a list of enabled modules
- `ITenantContext` / `ITenantResolver` in `BuildingBlocks.Tenancy` provide the abstraction
- The `DatabaseMigrator` takes a `--tenant <name>` CLI argument to target the right database

### Data Access

- Each module has its own `DbContext` (e.g., `InventoryDbContext`, `IdentityDbContext`) — no cross-module DB access
- EF Core Code-First migrations stored in each module's `Infrastructure` project
- Repository pattern sits between handlers and EF (e.g., `IProductRepository`)

### Authentication

- JWT tokens issued by the Identity module at `POST /auth/login`
- Default dev credentials seeded by `IdentitySeeder`: `admin@dev.local` / `Admin123!`
- JWT settings (issuer, audience, signing key, expiry) are in `appsettings.json` under `Jwt`

### Adding a New Module

1. Create four projects: `<Module>.Core`, `<Module>.UseCases`, `<Module>.Infrastructure`, `<Module>.Contracts`
2. Implement `IModuleInstaller` in the Infrastructure project
3. Register the installer in `SimpliaDz.Api/Program.cs` and `SimpliaDz.DatabaseMigrator`
4. Enable the module for relevant tenants in `appsettings.json`
